using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Runtime.Versioning;

namespace IdentityServer.ActiveDirectory
{
    [SupportedOSPlatform("windows")]
    public class Computer : ADObject
    {
        public Computer() { }
        public Computer(ADGuid guid) : base(guid) { }
        public Computer(DirectoryEntry entry) : base(entry) { }
        public Computer(String ldap) : base(ldap) { }

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

        /// <summary>
        /// Gets the object's description
        /// </summary>
        public String Description
        {
            get { return (String)base.adobject.Properties["description"].Value; }
        }

        public String DnsName
        {
            get { return (String)base.adobject.Properties["dNSHostName"].Value; }
        }

        /// <summary>
        /// Gets a List String containing the LDAP Group names this computer is a member of
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
        /// Enumation of computer account flags
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