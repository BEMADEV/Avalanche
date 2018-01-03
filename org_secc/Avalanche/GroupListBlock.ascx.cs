﻿using System;
using System.ComponentModel;
using Rock.Model;
using Rock.Security;
using System.Web.UI;
using Rock.Web.Cache;
using Rock.Web.UI;
using System.Web;
using Rock.Data;
using System.Linq;
using System.Collections.Generic;
using Rock;
using Avalanche;
using Avalanche.Models;
using Rock.Attribute;
using Newtonsoft.Json;

namespace RockWeb.Plugins.Avalanche
{
    [DisplayName( "Group List" )]
    [Category( "Avalanche" )]
    [Description( "Way to show list of groups" )]

    [LinkedPage( "Detail Page", "The page to navigate to for details.", false, "", "", 1 )]
    [TextField( "Parent Group Ids", "Comma separated list of id's of parent groups to look into." )]
    public partial class GroupListBlock : AvalancheBlock
    {

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summarysni>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            if ( !Page.IsPostBack )
            {
                RockContext rockContext = new RockContext();
                GroupService groupService = new GroupService( rockContext );

                var ids = new List<int>();
                var strings = GetAttributeValue( "ParentGroupIds" ).Split( new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries );
                foreach ( var s in strings )
                {
                    var id = s.AsInteger();
                    if ( id != 0 )
                    {
                        ids.Add( id );
                    }
                }

                var parentGroups = groupService.Queryable().Where( g => ids.Contains( g.Id ) ).Select( g => g.Name ).ToList();
                lbGroups.Text = String.Join( "<br>", parentGroups );
            }


        }

        public override MobileBlock GetMobile( string parameter )
        {
            var pageGuid = GetAttributeValue( "DetailPage" );
            PageCache page = PageCache.Read( pageGuid.AsGuid() );
            if ( page != null && page.IsAuthorized( "View", CurrentPerson ) )
            {
                CustomAttributes["DetailPage"] = page.Id.ToString();
            }

            CustomAttributes["Component"] = "Avalanche.Components.ListView.ColumnListView";

            return new MobileBlock()
            {
                BlockType = "Avalanche.Blocks.ListViewBlock",
                Attributes = CustomAttributes
            };
        }

        public override MobileBlockResponse HandleRequest( string request, Dictionary<string, string> Body )
        {
            if ( request != "" )
            {
                return new MobileBlockResponse()
                {
                    Request = request,
                    Response = JsonConvert.SerializeObject( new List<MobileListViewItem>() ),
                    TTL = GetAttributeValue( "OutputCacheDuration" ).AsInteger()
                };
            }

            RockContext rockContext = new RockContext();
            GroupService groupService = new GroupService( rockContext );

            var ids = new List<int>();
            var strings = GetAttributeValue( "ParentGroupIds" ).Split( new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries );
            foreach ( var s in strings )
            {
                var id = s.AsInteger();
                if ( id != 0 )
                {
                    ids.Add( id );
                }
            }
            var groupMemberService = new GroupMemberService( rockContext );

            int personId = 0;
            if ( CurrentPerson != null )
            {
                personId = CurrentPerson.Id;
            }

            var groups = groupService.Queryable()
                .Where( g => ids.Contains( g.Id ) )
                .SelectMany( g => g.Groups )
                .Join(
                    groupMemberService.Queryable(),
                    g => g.Id,
                    m => m.GroupId,
                    ( g, m ) => new { Group = g, Member = m }
                ).Where( m => m.Member.PersonId == personId && m.Member.GroupRole.IsLeader && m.Member.GroupMemberStatus == GroupMemberStatus.Active )
                .ToList() // leave sql server
                .Select( m => new MobileListViewItem
                {
                    Id = m.Group.Guid.ToString(),
                    Title = m.Group.Name,
                    Icon = m.Group.GroupType.IconCssClass,
                    Image = "",
                    Description = ""
                } )
                .DistinctBy( g => g.Id )
                .ToList();

            return new MobileBlockResponse()
            {
                Request = request,
                Response = JsonConvert.SerializeObject( groups ),
                TTL = GetAttributeValue( "OutputCacheDuration" ).AsInteger(),
            };
        }
    }
}