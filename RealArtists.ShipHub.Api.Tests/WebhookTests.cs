﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Hosting;
using System.Web.Http.Routing;
using AutoMapper;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RealArtists.ShipHub.Api.Controllers;
using RealArtists.ShipHub.Common.DataModel.Types;
using RealArtists.ShipHub.Common.GitHub;
using RealArtists.ShipHub.Common.GitHub.Models;
using RealArtists.ShipHub.QueueClient;
using Xunit;

namespace RealArtists.ShipHub.Api.Tests {
  public class WebhookTests {

    private static string SignatureForPayload(string key, string payload) {
      var hmac = new HMACSHA1(System.Text.Encoding.UTF8.GetBytes(key));
      byte[] hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
      return "sha1=" + BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
    }
    
    private static IMapper AutoMapper() {
      var config = new MapperConfiguration(cfg => {
        cfg.AddProfile<Common.DataModel.GitHubToDataModelProfile>();
      });
      var mapper = config.CreateMapper();
      return mapper;
    }

    private static async Task<IChangeSummary> CallHook(JObject obj) {
      var json = JsonConvert.SerializeObject(obj, GitHubClient.JsonSettings);
      var signature = SignatureForPayload("698DACE9-6267-4391-9B1C-C6F74DB43710", json);
      var webhookGuid = Guid.NewGuid();

      var config = new HttpConfiguration();
      var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/webhook/" + webhookGuid.ToString());
      request.Headers.Add("User-Agent", GitHubWebhookController.GitHubUserAgent);
      request.Headers.Add(GitHubWebhookController.EventHeaderName, "issues");
      request.Headers.Add(GitHubWebhookController.SignatureHeaderName, signature);
      request.Headers.Add(GitHubWebhookController.DeliveryIdHeaderName, Guid.NewGuid().ToString());
      request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(json));

      var route = config.Routes.MapHttpRoute("blah", "webhook/{rid}");
      var routeData = new HttpRouteData(route, new HttpRouteValueDictionary { { "controller", "test" } });

      IChangeSummary changeSummary = null;

      var mockBusClient = new Mock<IShipHubBusClient>();
      mockBusClient.Setup(x => x.NotifyChanges(It.IsAny<IChangeSummary>()))
        .Returns(Task.CompletedTask)
        .Callback((IChangeSummary arg) => { changeSummary = arg; });

      var controller = new GitHubWebhookController(mockBusClient.Object);
      controller.ControllerContext = new HttpControllerContext(config, routeData, request);
      controller.Request = request;
      controller.Request.Properties[HttpPropertyKeys.HttpConfigurationKey] = config;

      await controller.HandleHook(webhookGuid);

      return changeSummary;
    }

    private static JObject IssueChange(string action, Issue issue, long repositoryId) {
      return new JObject(
        new JProperty("action", "opened"),
        new JProperty("issue", JObject.FromObject(issue, JsonSerializer.CreateDefault(GitHubClient.JsonSettings))),
        new JProperty("sender", null),
        new JProperty("repository", new JObject(
          new JProperty("id", repositoryId)
          )),
        new JProperty("organization", null));
    }

    private static Common.DataModel.User MakeTestUser(Common.DataModel.ShipHubContext context) {
      var user = new Common.DataModel.User() {
        Id = 3001,
        Login = "aroon",
        Date = DateTimeOffset.Now,
      };
      context.Accounts.Add(user);
      return user;
    }

    private static Common.DataModel.Repository MakeTestRepo(Common.DataModel.ShipHubContext context, long accountId) {
      var repo = new Common.DataModel.Repository() {
        Id = 2001,
        Name = "myrepo",
        FullName = "aroon/myrepo",
        AccountId = accountId,
        Private = true,
        Date = DateTimeOffset.Now,
      };
      context.Repositories.Add(repo);
      return repo;
    }

    private static Common.DataModel.Issue MakeTestIssue(Common.DataModel.ShipHubContext context, long accountId, long repoId) {
      var issue = new Common.DataModel.Issue() {
        Id = 1001,
        UserId = accountId,
        RepositoryId = repoId,
        Number = 5,
        State = "open",
        Title = "Some Title",
        Body = "Some Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
      };
      context.Issues.Add(issue);
      return issue;
    }

    [Fact]
    [AutoRollback]
    public async Task TestIssueOpened() {
      Common.DataModel.User user;
      Common.DataModel.Repository repo;

      using (var context = new Common.DataModel.ShipHubContext()) {
        user = MakeTestUser(context);
        repo = MakeTestRepo(context, user.Id);
        await context.SaveChangesAsync();
      }
      
      var issue = new Issue() {
        Id = 1001,
        Title = "Some Title",
        Body = "Some Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        State = "open",
        Number = 1,
        Labels = new List<Label>(),
        User = new Account() {
          Id = user.Id,
          Login = user.Login,
          Type = GitHubAccountType.User,
        },
      };

      IChangeSummary changeSummary = await CallHook(IssueChange("opened", issue, repo.Id));

      Assert.Equal(0, changeSummary.Organizations.Count());
      Assert.Equal(new long[] { 2001 }, changeSummary.Repositories.ToArray());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var newIssue = context.Issues.First();
        Assert.Equal(1001, newIssue.Id);
        Assert.Equal(1, newIssue.Number);
        Assert.Equal("Some Title", newIssue.Title);
        Assert.Equal("Some Body", newIssue.Body);
        Assert.Equal("open", newIssue.State);
        Assert.Equal(2001, newIssue.RepositoryId);
        Assert.Equal(3001, newIssue.UserId);
      }
    }

    [Fact]
    [AutoRollback]
    public async Task TestIssueClosed() {
      Common.DataModel.User testUser;
      Common.DataModel.Repository testRepo;
      Common.DataModel.Issue testIssue;

      using (var context = new Common.DataModel.ShipHubContext()) {
        testUser = MakeTestUser(context);
        testRepo = MakeTestRepo(context, testUser.Id);
        testIssue = MakeTestIssue(context, testUser.Id, testRepo.Id);
        await context.SaveChangesAsync();
      }

      var issue = new Issue() {
        Id = 1001,
        Title = "Some Title",
        Body = "Some Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        State = "closed",
        Number = 5,
        Labels = new List<Label>(),
        User = new Account() {
          Id = testUser.Id,
          Login = testUser.Login,
          Type = GitHubAccountType.User,
        },
      };

      IChangeSummary changeSummary = await CallHook(IssueChange("closed", issue, testRepo.Id));

      Assert.Equal(0, changeSummary.Organizations.Count());
      Assert.Equal(new long[] { testRepo.Id }, changeSummary.Repositories.ToArray());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var updatedIssue = context.Issues.Where(x => x.Id == testIssue.Id).First();
        Assert.Equal("closed", updatedIssue.State);
      };    
    }

    [Fact]
    [AutoRollback]
    public async Task TestIssueReopened() {
      Common.DataModel.User testUser;
      Common.DataModel.Repository testRepo;
      Common.DataModel.Issue testIssue;

      using (var context = new Common.DataModel.ShipHubContext()) {
        testUser = MakeTestUser(context);
        testRepo = MakeTestRepo(context, testUser.Id);
        testIssue = MakeTestIssue(context, testUser.Id, testRepo.Id);

        testIssue.State = "closed";

        await context.SaveChangesAsync();
      }

      var issue = new Issue() {
        Id = 1001,
        Title = "Some Title",
        Body = "Some Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        State = "open",
        Number = 5,
        Labels = new List<Label>(),
        User = new Account() {
          Id = testUser.Id,
          Login = testUser.Login,
          Type = GitHubAccountType.User,
        },
      };

      IChangeSummary changeSummary = await CallHook(IssueChange("reopened", issue, testRepo.Id));

      Assert.Equal(0, changeSummary.Organizations.Count());
      Assert.Equal(new long[] { testRepo.Id }, changeSummary.Repositories.ToArray());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var updatedIssue = context.Issues.Where(x => x.Id == testIssue.Id).First();
        Assert.Equal("open", updatedIssue.State);
      }        
    }

    [Fact]
    [AutoRollback]
    public async Task TestIssueEdited() {
      Common.DataModel.User testUser;
      Common.DataModel.Repository testRepo;
      Common.DataModel.Issue testIssue;

      using (var context = new Common.DataModel.ShipHubContext()) {
        testUser = MakeTestUser(context);
        testRepo = MakeTestRepo(context, testUser.Id);
        testIssue = MakeTestIssue(context, testUser.Id, testRepo.Id);
        await context.SaveChangesAsync();
      }

      var issue = new Issue() {
        Id = 1001,
        Title = "A New Title",
        Body = "A New Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        State = "open",
        Number = 5,
        Labels = new List<Label> {
          new Label() {
            Color = "ff0000",
            Name = "Red",
          },
          new Label() {
            Color = "0000ff",
            Name = "Blue",
          },
        },
        User = new Account() {
          Id = testUser.Id,
          Login = testUser.Login,
          Type = GitHubAccountType.User,
        },
      };

      IChangeSummary changeSummary = await CallHook(IssueChange("edited", issue, testRepo.Id));

      Assert.Equal(0, changeSummary.Organizations.Count());
      Assert.Equal(new long[] { testRepo.Id }, changeSummary.Repositories.ToArray());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var updatedIssue = context.Issues.Where(x => x.Id == testIssue.Id).First();
        Assert.Equal("A New Title", updatedIssue.Title);
        Assert.Equal("A New Body", updatedIssue.Body);
      };
    }

    [Fact]
    [AutoRollback]
    public async Task TestIssueAssigned() {
      Common.DataModel.User testUser;
      Common.DataModel.Repository testRepo;
      Common.DataModel.Issue testIssue;

      using (var context = new Common.DataModel.ShipHubContext()) {
        testUser = MakeTestUser(context);
        testRepo = MakeTestRepo(context, testUser.Id);
        testIssue = MakeTestIssue(context, testUser.Id, testRepo.Id);
        await context.SaveChangesAsync();
      }

      var issue = new Issue() {
        Id = 1001,
        Title = "Some Title",
        Body = "Some Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        State = "closed",
        Number = 5,
        Labels = new List<Label>(),
        User = new Account() {
          Id = testUser.Id,
          Login = testUser.Login,
          Type = GitHubAccountType.User,
        },
        Assignee = new Account() {
          Id = testUser.Id,
          Login = testUser.Login,
          Type = GitHubAccountType.User,
        },
      };

      IChangeSummary changeSummary = await CallHook(IssueChange("assigned", issue, testRepo.Id));

      Assert.Equal(0, changeSummary.Organizations.Count());
      Assert.Equal(new long[] { testRepo.Id }, changeSummary.Repositories.ToArray());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var updatedIssue = context.Issues.Where(x => x.Id == testIssue.Id).First();      
        Assert.Equal(testUser.Id, updatedIssue.Assignee.Id);
      }
    }

    [Fact]
    [AutoRollback]
    public async Task TestIssueUnassigned() {
      Common.DataModel.User testUser;
      Common.DataModel.Repository testRepo;
      Common.DataModel.Issue testIssue;

      using (var context = new Common.DataModel.ShipHubContext()) {
        testUser = MakeTestUser(context);
        testRepo = MakeTestRepo(context, testUser.Id);
        testIssue = MakeTestIssue(context, testUser.Id, testRepo.Id);

        testIssue.Assignee = testUser;

        await context.SaveChangesAsync();
      }

      var issue = new Issue() {
        Id = 1001,
        Title = "Some Title",
        Body = "Some Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        State = "closed",
        Number = 5,
        Labels = new List<Label>(),
        User = new Account() {
          Id = testUser.Id,
          Login = testUser.Login,
          Type = GitHubAccountType.User,
        },
        Assignee = null,
      };

      IChangeSummary changeSummary = await CallHook(IssueChange("unassigned", issue, testRepo.Id));

      Assert.Equal(0, changeSummary.Organizations.Count());
      Assert.Equal(new long[] { testRepo.Id }, changeSummary.Repositories.ToArray());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var updatedIssue = context.Issues.Where(x => x.Id == testIssue.Id).First();
        Assert.Null(updatedIssue.Assignee);
      }
    }

    [Fact]
    [AutoRollback]
    public async Task TestIssueLabeled() {
      Common.DataModel.User testUser;
      Common.DataModel.Repository testRepo;
      Common.DataModel.Issue testIssue;

      using (var context = new Common.DataModel.ShipHubContext()) {
        testUser = MakeTestUser(context);
        testRepo = MakeTestRepo(context, testUser.Id);
        testIssue = MakeTestIssue(context, testUser.Id, testRepo.Id);
        await context.SaveChangesAsync();
      }

      var issue = new Issue() {
        Id = 1001,
        Title = "Some Title",
        Body = "Some Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        State = "open",
        Number = 5,
        Labels = new List<Label> {
          new Label() {
            Color = "ff0000",
            Name = "Red",
          },
          new Label() {
            Color = "0000ff",
            Name = "Blue",
          },
        },
        User = new Account() {
          Id = testUser.Id,
          Login = testUser.Login,
          Type = GitHubAccountType.User,
        },
      };

      IChangeSummary changeSummary = await CallHook(IssueChange("labeled", issue, testRepo.Id));

      Assert.Equal(0, changeSummary.Organizations.Count());
      Assert.Equal(new long[] { testRepo.Id }, changeSummary.Repositories.ToArray());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var updatedIssue = context.Issues.Where(x => x.Id == testIssue.Id).First();
        var labels = updatedIssue.Labels.OrderBy(x => x.Name).ToArray();
        Assert.Equal(2, labels.Count());
        Assert.Equal("Blue", labels[0].Name);
        Assert.Equal("0000ff", labels[0].Color);
        Assert.Equal("Red", labels[1].Name);
        Assert.Equal("ff0000", labels[1].Color);
      };
    }

    [Fact]
    [AutoRollback]
    public async Task TestIssueUnlabeled() {
      Common.DataModel.User testUser;
      Common.DataModel.Repository testRepo;
      Common.DataModel.Issue testIssue;

      using (var context = new Common.DataModel.ShipHubContext()) {
        testUser = MakeTestUser(context);
        testRepo = MakeTestRepo(context, testUser.Id);
        testIssue = MakeTestIssue(context, testUser.Id, testRepo.Id);
        await context.SaveChangesAsync();
      }

      // First add the labels Red and Blue
      var issue = new Issue() {
        Id = 1001,
        Title = "Some Title",
        Body = "Some Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        State = "open",
        Number = 5,
        Labels = new List<Label> {
          new Label() {
            Color = "ff0000",
            Name = "Red",
          },
          new Label() {
            Color = "0000ff",
            Name = "Blue",
          },
        },
        User = new Account() {
          Id = testUser.Id,
          Login = testUser.Login,
          Type = GitHubAccountType.User,
        },
      };

      IChangeSummary changeSummary = await CallHook(IssueChange("edited", issue, testRepo.Id));

      Assert.Equal(0, changeSummary.Organizations.Count());
      Assert.Equal(new long[] { testRepo.Id }, changeSummary.Repositories.ToArray());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var updatedIssue = context.Issues.Where(x => x.Id == testIssue.Id).First();
        var labels = updatedIssue.Labels.OrderBy(x => x.Name).ToArray();
        Assert.Equal(2, labels.Count());
      };

      // Then remove the Red label.
      issue.Labels = issue.Labels.Where(x => !x.Name.Equals("Red"));
      changeSummary = await CallHook(IssueChange("unlabeled", issue, testRepo.Id));

      Assert.Equal(0, changeSummary.Organizations.Count());
      Assert.Equal(0, changeSummary.Repositories.Count());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var updatedIssue = context.Issues.Where(x => x.Id == testIssue.Id).First();
        var labels = updatedIssue.Labels.OrderBy(x => x.Name).ToArray();
        Assert.Equal(1, labels.Count());
        Assert.Equal("Blue", labels[0].Name);
        Assert.Equal("0000ff", labels[0].Color);
      };
    }
  }
}
