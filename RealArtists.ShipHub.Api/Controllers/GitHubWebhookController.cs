﻿namespace RealArtists.ShipHub.Api.Controllers {
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.IO;
  using System.Linq;
  using System.Net;
  using System.Runtime.Remoting.Metadata.W3cXsd2001;
  using System.Security.Cryptography;
  using System.Text;
  using System.Threading.Tasks;
  using System.Web.Http;
  using AutoMapper;
  using Common.DataModel;
  using Common.DataModel.Types;
  using Common.GitHub;
  using Newtonsoft.Json;
  using Newtonsoft.Json.Linq;
  using QueueClient;

  [AllowAnonymous]
  public class GitHubWebhookController : ApiController {
    public const string GitHubUserAgent = "GitHub-Hookshot";
    public const string EventHeaderName = "X-GitHub-Event";
    public const string DeliveryIdHeaderName = "X-GitHub-Delivery";
    public const string SignatureHeaderName = "X-Hub-Signature";

    private IShipHubBusClient _busClient;

    public GitHubWebhookController() : this(new ShipHubBusClient()) {
    }

    public GitHubWebhookController(IShipHubBusClient busClient) {
      _busClient = busClient;
    }

    [HttpPost]
    [AllowAnonymous]
    [Route("webhook")]
    public async Task<IHttpActionResult> HandleHook() {
      if (Request.Headers.UserAgent.Single().Product.Name != GitHubUserAgent) {
        return BadRequest("Not you.");
      }

      var eventName = Request.Headers.GetValues(EventHeaderName).Single();
      var deliveryIdHeader = Request.Headers.GetValues(DeliveryIdHeaderName).Single();
      var signatureHeader = Request.Headers.GetValues(SignatureHeaderName).Single();

      // header of form "sha1=..."
      byte[] signature = SoapHexBinary.Parse(signatureHeader.Substring(5)).Value;
      var deliveryId = Guid.Parse(deliveryIdHeader);

      var json = await Request.Content.ReadAsStringAsync();
      var data = JsonConvert.DeserializeObject<JObject>(json, GitHubClient.JsonSettings);

      long? repositoryId = null;
      long? organizationId = null;
      
      if (
        data["repository"] != null &&
        data["repository"].Type == JTokenType.Object &&
        data["repository"]["id"] != null &&
        data["repository"]["id"].Type == JTokenType.Integer) {
        repositoryId = data["repository"]["id"].ToObject<long>();
      }

      if (
        data["organization"] != null &&
        data["organization"].Type == JTokenType.Object &&
        data["organization"]["id"] != null &&
        data["organization"]["id"].Type == JTokenType.Integer) {
        organizationId = data["organization"]["id"].ToObject<long>();
      }

      if (repositoryId == null && organizationId == null) {
        return BadRequest("Payload must include repository and/or organization objects.");
      }

      var context = new ShipHubContext();
      var hooks = context.Hooks
        .Where(x => (repositoryId != null && x.RepositoryId == repositoryId) || (organizationId != null && x.OrganizationId == organizationId))
        .ToList();

      string payload = await Request.Content.ReadAsStringAsync();
      Hook matchingHook = null;

      foreach (var hook in hooks) {
        Debug.Assert(repositoryId == hook.RepositoryId || organizationId == hook.OrganizationId);
        var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(hook.Secret.ToString()));
        byte[] hash = hmac.ComputeHash(await Request.Content.ReadAsStreamAsync());
        // We're not worth launching a timing attack against.
        if (hash.SequenceEqual(signature)) {
          matchingHook = hook;
          break;
        }
      }

      if (matchingHook == null) {
        return BadRequest("Invalid signature.");
      }

      matchingHook.LastSeen = DateTimeOffset.Now;
      await context.SaveChangesAsync();
      
      switch (eventName) {
        case "issues":
          var actions = new string[] {
            "opened",
            "closed",
            "reopened",
            "edited",
            "labeled",
            "unlabeled",
            "assigned",
            "unassigned",
          };

          if (actions.Contains(data["action"].ToString())) {
            await HandleIssueUpdate(data);
          }
          break;
        case "ping":
          break;
        default:
          return StatusCode(HttpStatusCode.InternalServerError);
      }

      return StatusCode(HttpStatusCode.Accepted);
    }

    private async Task HandleIssueUpdate(JObject data) {
      var serializer = JsonSerializer.CreateDefault(GitHubClient.JsonSettings);

      var item = data["issue"].ToObject<Common.GitHub.Models.Issue>(serializer);
      long repositoryId = data["repository"]["id"].Value<long>();

      var issues = new List<Common.GitHub.Models.Issue> { item };

      var config = new MapperConfiguration(cfg => {
        cfg.AddProfile<GitHubToDataModelProfile>();
      });
      var mapper = config.CreateMapper();
      var issuesMapped = mapper.Map<IEnumerable<IssueTableType>>(issues);
      
      var labels = item.Labels.Select(x => new LabelTableType() {
        Id = item.Id,
        Color = x.Color,
        Name = x.Name
      });

      var context = new ShipHubContext();
      ChangeSummary changeSummary = await context.BulkUpdateIssues(repositoryId, issuesMapped, labels);

      await _busClient.NotifyChanges(changeSummary);
    }
  }
}