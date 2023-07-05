using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Runtime.Versioning;

namespace IdentityServer.ActiveDirectory
{
    /// <summary>
    /// Represents a user represented by a logon account name
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class User : ADObject
    {
        public User() { }

        /// <summary>
        /// Class constructor. Accepts a user's logon name or distinguished name and gets the associated user account in active directory.
        /// </summary>
        /// <param name="userName"></param>
        public User(String userName)
        {
            if (userName == null || userName == "")
                throw new ArgumentNullException();

            if (userName.Contains("LDAP://"))
            {
                base.adobject = new DirectoryEntry(userName);
            }
            else
            {
                if (userName.Contains("\\"))
                {
                    userName = userName.Split('\\')[1];
                }

                DirectorySearcher search = new DirectorySearcher();
                search.Filter = "(&(objectClass=user)(sAMAccountName=" + CleanLDAPString(userName) + "))";
                search.PropertiesToLoad.Add("cn");
                SearchResult result = search.FindOne();
                try
                {
                    base.adobject = result.GetDirectoryEntry();
                }
                catch (Exception) { }
            }
        }

        public User(ADGuid guid) : base(guid) { }
        public User(DirectoryEntry entry) : base(entry) { }

        /// <summary>
        /// Gets true or false depending if the given flag is set or not.
        /// </summary>
        /// <param name="userDn"></param>
        /// <param name="uacflag"></param>
        /// <returns></returns>
        public bool UACValue(UACFlags uacflag)
        {
            if (((int)(base.adobject.Properties["userAccountControl"].Value) & (int)uacflag) != 0)
            {
                return true;
            }
            return false;
        }

        #region General Tab
        /// <summary>
        /// Gets or sets the user's given name
        /// </summary>
        public String FirstName
        {
            get { return (String)base.adobject.Properties["givenName"].Value; }
            set { base.adobject.Properties["givenName"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's last name
        /// </summary>
        public String LastName
        {
            get { return (String)base.adobject.Properties["sn"].Value; }
            set { base.adobject.Properties["sn"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's initials
        /// </summary>
        public String Initials
        {
            get { return (String)base.adobject.Properties["initials"].Value; }
            set { base.adobject.Properties["initials"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's display name
        /// </summary>
        public string DisplayName
        {
            get { return (String)base.adobject.Properties["displayName"].Value; }
            set { base.adobject.Properties["displayName"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's description
        /// </summary>
        public String Description
        {
            get { return (String)base.adobject.Properties["description"].Value; }
            set { base.adobject.Properties["description"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's office location
        /// </summary>
        public String Office
        {
            get { return (String)base.adobject.Properties["physicalDeliveryOfficeName"].Value; }
            set { base.adobject.Properties["physicalDeliveryOfficeName"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's telephone number
        /// </summary>
        public String TelephoneNumber
        {
            get { return (String)base.adobject.Properties["telephoneNumber"].Value; }
            set { base.adobject.Properties["telephoneNumber"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's email address
        /// </summary>
        public String Email
        {
            get { return (String)base.adobject.Properties["mail"].Value; }
            set { base.adobject.Properties["mail"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's web page
        /// </summary>
        public String WebPage
        {
            get { return (String)base.adobject.Properties["wWWHomePage"].Value; }
            set { base.adobject.Properties["wWWHomePage"].Value = value.Length > 0 ? value : null; }
        }
        #endregion

        #region Address Tab
        /// <summary>
        /// Gets or sets the user's street address
        /// </summary>
        public String Street
        {
            get { return (String)base.adobject.Properties["street"].Value; }
            set { base.adobject.Properties["street"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's PO box
        /// </summary>
        public String POBox
        {
            get { return (String)base.adobject.Properties["postOfficeBox"].Value; }
            set { base.adobject.Properties["postOfficeBox"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's city
        /// </summary>
        public String City
        {
            get { return (String)base.adobject.Properties["l"].Value; }
            set { base.adobject.Properties["l"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's province or state
        /// </summary>
        public String Province
        {
            get { return (String)base.adobject.Properties["st"].Value; }
            set { base.adobject.Properties["st"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's postal code
        /// </summary>
        public String PostalCode
        {
            get { return (String)base.adobject.Properties["postalCode"].Value; }
            set { base.adobject.Properties["postalCode"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's country
        /// </summary>
        public String Country
        {
            get { return (String)base.adobject.Properties["co"].Value; }
            set { base.adobject.Properties["co"].Value = value.Length > 0 ? value : null; }
        }
        #endregion

        #region Telephone Tab
        /// <summary>
        /// Gets or sets the user's home phone number
        /// </summary>
        public String HomePhone
        {
            get { return (String)base.adobject.Properties["homePhone"].Value; }
            set { base.adobject.Properties["homePhone"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's pager number
        /// </summary>
        public String Pager
        {
            get { return (String)base.adobject.Properties["pager"].Value; }
            set { base.adobject.Properties["pager"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's mobile phone number
        /// </summary>
        public String MobilePhone
        {
            get { return (String)base.adobject.Properties["mobile"].Value; }
            set { base.adobject.Properties["mobile"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's fax number
        /// </summary>
        public String FaxNumber
        {
            get { return (String)base.adobject.Properties["facsimileTelephoneNumber"].Value; }
            set { base.adobject.Properties["facsimileTelephoneNumber"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's IP Phone number
        /// </summary>
        public String IPPhone
        {
            get { return (String)base.adobject.Properties["ipPhone"].Value; }
            set { base.adobject.Properties["ipPhone"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's notes field
        /// </summary>
        public String Notes
        {
            get { return (String)base.adobject.Properties["notes"].Value; }
            set { base.adobject.Properties["notes"].Value = value.Length > 0 ? value : null; }
        }
        #endregion

        #region Organization Tab
        /// <summary>
        /// Gets or sets the user's title
        /// </summary>
        public String Title
        {
            get { return (String)base.adobject.Properties["title"].Value; }
            set { base.adobject.Properties["title"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's department
        /// </summary>
        public String Department
        {
            get { return (String)base.adobject.Properties["department"].Value; }
            set { base.adobject.Properties["department"].Value = value.Length > 0 ? value : null; }
        }

        /// <summary>
        /// Gets or sets the user's company
        /// </summary>
        public String Company
        {
            get { return (String)base.adobject.Properties["company"].Value; }
            set { base.adobject.Properties["company"].Value = value.Length > 0 ? value : null; }
        }
        #endregion

        #region Additional Attributes
        public String EmployeeID
        {
            get { return (String)base.adobject.Properties["employeeID"].Value; }
            set { base.adobject.Properties["employeeID"].Value = value.Length > 0 ? value : null; }
        }

        public String EmployeeNumber
        {
            get { return (String)base.adobject.Properties["employeeNumber"].Value; }
            set { base.adobject.Properties["employeeNumber"].Value = value.Length > 0 ? value : null; }
        }
        #endregion

        #region Active Directory Settings
        public String LogonScript
        {
            get { return (String)base.adobject.Properties["scriptPath"].Value; }
            set { base.adobject.Properties["scriptPath"].Value = value.Length > 0 ? value : null; }
        }

        public String HomeDirectory
        {
            get { return (String)base.adobject.Properties["homeDirectory"].Value; }
            set { base.adobject.Properties["homeDirectory"].Value = value.Length > 0 ? value : null; }
        }

        public String Username
        {
            get { return (String)base.adobject.Properties["sAMAccountName"].Value; }
        }

        public String ExchangeServerGroup
        {
            get { return (String)base.adobject.Properties["homemta"].Value; }
        }

        public String ProfilePath
        {
            get { return (String)base.adobject.Properties["profilePath"].Value; }
        }
        #endregion

        /// <summary>
        /// Gets a List String containing the LDAP Group names this user is a member of
        /// </summary>
        public List<String> Groups
        {
            get
            {
                List<String> result = new List<String>();
                PropertyValueCollection values = base.adobject.Properties["memberOf"];
                IEnumerator en = values.GetEnumerator();

                while (en.MoveNext())
                {
                    if (en.Current != null)
                    {
                        result.Add(en.Current.ToString());
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Same as Groups, but only returns the common name rather than the whole LDAP string
        /// </summary>
        public List<String> GroupsCommonName
        {
            get
            {
                List<String> result = new List<String>();

                foreach (String group in Groups)
                {
                    foreach (String el in group.Split(new char[] { ',' }))
                    {
                        if (el.StartsWith("CN="))
                        {
                            result.Add(el.Substring(3));
                        }
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Enumation of user account flags
        /// </summary>
        public enum UACFlags
        {
            SCRIPT = 0x0001,
            ACCOUNTDISABLE = 0x0002,
            HOMEDIR_REQUIRED = 0x0008,
            LOCKOUT = 0x0010,
            PASSWD_NOTREQD = 0x0020,
            PASSWD_CANT_CHANGE = 0x0040,
            ENCRYPTED_TEXT_PWD_ALLOWED = 0x0080,
            TEMP_DUPLICATE_ACCOUNT = 0x0100,
            NORMAL_ACCOUNT = 0x0200,
            INTERDOMAIN_TRUST_ACCOUNT = 0x0800,
            WORKSTATION_TRUST_ACCOUNT = 0x1000,
            SERVER_TRUST_ACCOUNT = 0x2000,
            DONT_EXPIRE_PASSWORD = 0x10000,
            MNS_LOGON_ACCOUNT = 0x20000,
            SMARTCARD_REQUIRED = 0x40000,
            TRUSTED_FOR_DELEGATION = 0x80000,
            NOT_DELEGATED = 0x100000,
            USE_DES_KEY_ONLY = 0x200000,
            DONT_REQ_PREAUTH = 0x400000,
            PASSWORD_EXPIRED = 0x800000,
            TRUSTED_TO_AUTH_FOR_DELEGATION = 0x1000000
        };
    }
}
