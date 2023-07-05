using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Runtime.Versioning;

namespace IdentityServer.ActiveDirectory
{
    /// <summary>
    /// Represents a user represented by a logon account name
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class Group : ADObject
    {
        public Group() { }
        public Group(ADGuid guid) : base(guid) { }
        public Group(DirectoryEntry entry) : base(entry) { }
        public Group(String ldap) : base(ldap) { }

        /// <summary>
        /// Gets a string containing this group's description
        /// </summary>
        public String Description
        {
            get { return (String)base.adobject.Properties["description"].Value; }
        }

        public String Notes
        {
            get { return (String)base.adobject.Properties["info"].Value; }
        }

        public ADObject managedBy
        {
            get { return base.adobject.Properties["managedBy"].Value == null ? null : new ADObject(base.adobject.Properties["managedBy"].Value.ToString()); }
        }

        public String Windows2000Name
        {
            get { return (String)base.adobject.Properties["sAMAccountName"].Value; }
        }

        /// <summary>
        /// Adds an active directory object to a group
        /// </summary>
        /// <param name="obj"></param>
        public void addMember(ADObject obj)
        {
            base.adobject.Properties["member"].Add(obj.LDAPName);
        }

        /// <summary>
        /// Removes an active directory object from a group
        /// </summary>
        /// <param name="obj"></param>
        public void removeMember(ADObject obj)
        {
            base.adobject.Properties["member"].Remove(obj.LDAPName);
        }

        /// <summary>
        /// Gets a list of the group's members
        /// </summary>
        public List<Membership> Members
        {
            get
            {
                List<Membership> info = new List<Membership>();

                // Find all member groups
                DirectorySearcher groupMembers = new DirectorySearcher("(&(objectCategory=group)(memberOf=<GUID=" + base.ADGuid.ToString() + ">))");
                groupMembers.PropertiesToLoad.Add("cn");
                groupMembers.PropertiesToLoad.Add("objectguid");
                groupMembers.PropertiesToLoad.Add("distinguishedname");

                foreach (SearchResult result in groupMembers.FindAll())
                {
                    info.Add(new Membership() { ObjectType = Membership.Class.Group, DisplayName = result.Properties["cn"][0].ToString(), DistinguishedName = result.Properties["distinguishedname"][0].ToString(), ADGuid = new ADGuid(result.Properties["objectguid"][0] as byte[]) });
                }

                // Find all member users
                groupMembers = new DirectorySearcher("(&(objectCategory=user)(memberOf=<GUID=" + base.ADGuid.ToString() + ">))");
                groupMembers.PropertiesToLoad.Add("displayname");
                groupMembers.PropertiesToLoad.Add("objectguid");
                groupMembers.PropertiesToLoad.Add("distinguishedname");

                foreach (SearchResult result in groupMembers.FindAll())
                {
                    Guid nativeGuid = new Guid(result.Properties["objectguid"][0] as byte[]);
                    String distinguishedName = result.Properties["distinguishedname"][0].ToString();
                    String displayName = result.Properties["displayname"].Count > 0 ? result.Properties["displayname"][0].ToString() : "<" + distinguishedName + ">";

                    info.Add(new Membership() { ObjectType = Membership.Class.User, DisplayName = displayName, DistinguishedName = distinguishedName, ADGuid = new ADGuid(result.Properties["objectguid"][0] as byte[]) });
                }

                // Find all member computers
                groupMembers = new DirectorySearcher("(&(objectCategory=computer)(memberOf=<GUID=" + base.ADGuid.ToString() + ">))");
                groupMembers.PropertiesToLoad.Add("cn");
                groupMembers.PropertiesToLoad.Add("objectguid");
                groupMembers.PropertiesToLoad.Add("distinguishedname");

                foreach (SearchResult result in groupMembers.FindAll())
                {
                    Guid nativeGuid = new Guid(result.Properties["objectguid"][0] as byte[]);

                    info.Add(new Membership() { ObjectType = Membership.Class.Computer, DisplayName = result.Properties["cn"][0].ToString(), DistinguishedName = result.Properties["distinguishedname"][0].ToString(), ADGuid = new ADGuid(result.Properties["objectguid"][0] as byte[]) });
                }

                return info;
            }
        }

        public List<Membership> Parents
        {
            get
            {
                List<Membership> info = new List<Membership>();

                DirectorySearcher parents = new DirectorySearcher("(member:" + LDAP_MATCHING_RULE_IN_CHAIN + ":=<GUID=" + base.ADGuid.ToString() + ">)");
                parents.SearchScope = SearchScope.Subtree;
                parents.SearchRoot = new DirectoryEntry("LDAP://OU=CLK,OU=ACCOUNTS,DC=FORCES,DC=MIL,DC=CA");
                parents.PropertiesToLoad.Add("cn");
                parents.PropertiesToLoad.Add("objectguid");
                parents.PropertiesToLoad.Add("distinguishedname");

                foreach (SearchResult result in parents.FindAll())
                {
                    ADGuid nativeGuid = new ADGuid(result.Properties["objectguid"][0] as byte[]);

                    info.Add(new Membership() { ObjectType = Membership.Class.Computer, DisplayName = result.Properties["cn"][0].ToString(), DistinguishedName = result.Properties["distinguishedname"][0].ToString(), ADGuid = new ADGuid(result.Properties["objectguid"][0] as byte[]) });
                }

                return info;
            }
        }

        public class Membership
        {
            public string DistinguishedName;
            public string DisplayName;
            public ADGuid ADGuid;
            public Class ObjectType;
            public enum Class { User, Group, Computer };
        }

        public enum GroupType : uint
        {
            UniversalGroup = 0x08,
            DomainLocalGroup = 0x04,
            GlobalGroup = 0x02,
            SecurityGroup = 0x80000000
        }
    }
}
