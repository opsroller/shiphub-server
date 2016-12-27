﻿#pragma warning disable 1591
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace RealArtists.ShipHub.Mail.Views
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    
    #line 2 "..\..\Views\PurchasePersonalHtml.cshtml"
    using RealArtists.ShipHub.Mail;
    
    #line default
    #line hidden
    
    #line 3 "..\..\Views\PurchasePersonalHtml.cshtml"
    using RealArtists.ShipHub.Mail.Models;
    
    #line default
    #line hidden
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("RazorGenerator", "2.0.0.0")]
    public partial class PurchasePersonalHtml : ShipHubTemplateBase<PurchasePersonalMailMessage>
    {
#line hidden

        public override void Execute()
        {


WriteLiteral("\r\n");





            
            #line 5 "..\..\Views\PurchasePersonalHtml.cshtml"
  
  Layout = new RealArtists.ShipHub.Mail.Views.LayoutHtml() { Model = Model };


            
            #line default
            #line hidden
WriteLiteral("<p>\r\n    Thanks for purchasing a subscription to Ship - we hope you enjoy using i" +
"t!\r\n</p>\r\n<p>\r\n    <a href=\"");


            
            #line 12 "..\..\Views\PurchasePersonalHtml.cshtml"
        Write(Model.InvoicePdfUrl);

            
            #line default
            #line hidden
WriteLiteral("\">Download a PDF receipt</a> for your records.\r\n</p>\r\n");


            
            #line 14 "..\..\Views\PurchasePersonalHtml.cshtml"
 if (Model.WasGivenTrialCredit) {

            
            #line default
            #line hidden
WriteLiteral("<p>\r\n    A discount was applied to your first invoice becuase you still had some " +
"time\r\n    remaining on your free trial.  Next month you\'ll see the regular price" +
" of $9/month.\r\n</p>\r\n");


            
            #line 19 "..\..\Views\PurchasePersonalHtml.cshtml"
}

            
            #line default
            #line hidden
WriteLiteral("\r\n");


            
            #line 21 "..\..\Views\PurchasePersonalHtml.cshtml"
 if (Model.BelongsToOrganization) {

            
            #line default
            #line hidden
WriteLiteral("<h4>Want to use Ship for free?</h4>\r\n");



WriteLiteral("<p>\r\n    Your personal Ship subscription is free as long as you belong\r\n    to an" +
" organization that subscribes to Ship.  Ask your organization to sign up.\r\n</p>\r" +
"\n");


            
            #line 27 "..\..\Views\PurchasePersonalHtml.cshtml"
}

            
            #line default
            #line hidden
WriteLiteral(@"
<h4>How to manage your account:</h4>
<p class=""last"">
    If you need to change billing or payment info, or need to cancel your account, you can do so
    from within the Ship application. From the <em>Ship</em> menu,
    choose <em>Manage Subscription</em>.  Then click <em>Manage</em> for
    your account.
</p>");


        }
    }
}
#pragma warning restore 1591