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
    
    #line 2 "..\..\Views\CancellationScheduledHtml.cshtml"
    using RealArtists.ShipHub.Mail;
    
    #line default
    #line hidden
    
    #line 3 "..\..\Views\CancellationScheduledHtml.cshtml"
    using RealArtists.ShipHub.Mail.Models;
    
    #line default
    #line hidden
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("RazorGenerator", "2.0.0.0")]
    public partial class CancellationScheduledHtml : ShipHubTemplateBase<CancellationScheduledMailMessage>
    {
#line hidden

        public override void Execute()
        {


WriteLiteral("\r\n");





            
            #line 5 "..\..\Views\CancellationScheduledHtml.cshtml"
  
  Layout = new RealArtists.ShipHub.Mail.Views.LayoutHtml() { Model = Model };


            
            #line default
            #line hidden
WriteLiteral("<p>\r\n    This is a confirmation that your Ship subscription will cancel on\r\n    <" +
"strong>");


            
            #line 10 "..\..\Views\CancellationScheduledHtml.cshtml"
        Write(Model.CurrentTermEnd.ToString("MMM d, yyyy"));

            
            #line default
            #line hidden
WriteLiteral("</strong>.\r\n</p>\r\n<p class=\"last\">\r\n    Thank you for your business!\r\n</p>\r\n");


        }
    }
}
#pragma warning restore 1591
