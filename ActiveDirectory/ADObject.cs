using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Runtime.Versioning;

namespace IdentityServer.ActiveDirectory
{
    [SupportedOSPlatform("windows")]
    public class ADObject : IDisposable
    {
        public const string LDAP_MATCHING_RULE_BIT_AND = "1.2.840.113556.1.4.803"; // A match is found only if all bits from the attribute match the value. This rule is equivalent to a bitwise AND operator. 
        public const string LDAP_MATCHING_RULE_BIT_OR = "1.2.840.113556.1.4.804"; // A match is found if any bits from the attribute match the value. This rule is equivalent to a bitwise OR operator. 
        public const string LDAP_MATCHING_RULE_IN_CHAIN = "1.2.840.113556.1.4.1941"; // This rule is limited to filters that apply to the DN. This is a special "extended match operator that walks the chain of ancestry in objects all the way to the root until it finds a match. 

        protected DirectoryEntry adobject;
        private bool disposedValue;

        public ADObject() { }

        /// <summary>
        /// Class constructor. Accepts a user's logon name or distinguished name and gets the associated user account in active directory.
        /// </summary>
        /// <param name="userName"></param>
        public ADObject(String ldap)
        {
            if (ldap == null || ldap == "")
                throw new ArgumentNullException();

            if (ldap.Contains("LDAP://"))
            {
                adobject = new DirectoryEntry(ldap);
            }
            else
            {
                adobject = new DirectoryEntry("LDAP://" + ldap);
            }
        }

        public ADObject(DirectoryEntry adobject)
        {
            this.adobject = adobject;
        }

        public ADObject(ADGuid guid)
        {
            adobject = new DirectoryEntry("LDAP://<GUID=" + guid.ToString() + ">");
        }

        public String SchemaClassName()
        {
            return adobject.SchemaClassName;
        }

        public bool IsUser()
        {
            return adobject.SchemaClassName.Equals("user");
        }

        public bool IsGroup()
        {
            return adobject.SchemaClassName.Equals("group");
        }

        public DirectoryEntry Get
        {
            get { return adobject; }
        }

        public ADGuid ADGuid
        {
            get
            {
                return new ADGuid(adobject.NativeGuid);
            }
        }

        /// <summary>
        /// Gets the object's common name
        /// </summary>
        public String Name
        {
            get { return (String)adobject.Properties["cn"].Value; }
        }

        /// <summary>
        /// Gets the object's fully qualified distinguished name
        /// </summary>
        public String QualifiedName
        {
            get { return (String)adobject.Properties["distinguishedName"].Value; }
        }

        /// <summary>
        /// Gets the object's LDAP name
        /// </summary>
        public String LDAPName
        {
            get { return adobject.Name; }
        }

        /// <summary>
        /// Gets the object's creation time
        /// (Not replicated; creation time depends on the queried DC)
        /// </summary>
        public DateTime WhenCreated
        {
            get { return (DateTime)adobject.Properties["whenCreated"].Value; }
        }

        /// <summary>
        /// Gets the object's last update time
        /// (Not replicated; update time can be null; depends on queried DC)
        /// </summary>
        public DateTime? WhenChanged
        {
            get { return (DateTime?)adobject.Properties["whenChanged"].Value; }
        }

        /// <summary>
        /// Returns a List&lt;String&gt; of properties the user object contains
        /// </summary>
        public List<String> Attributes
        {
            get
            {
                List<String> list = new List<String>();

                foreach (string prop in adobject.Properties.PropertyNames)
                {
                    list.Add(prop);
                }

                return list;
            }
        }

        /// <summary>
        /// Accessor method to retrieve an arbitrary property by name
        /// </summary>
        /// <param name="name">Property name</param>
        /// <returns>The answer or an empty string if no such property exists or property is not a string</returns>
        public String GetProperty(String propertyName)
        {
            try
            {
                return (string)adobject.Properties[propertyName].Value;
            }
            catch (Exception)
            {
                return "";
            }
        }

        public List<String> GetList(String property)
        {
            if (adobject.Properties[property].Value == null) { return new List<String>(); }
            else
            {
                if (adobject.Properties[property].Value is Array)
                {
                    return ((Array)adobject.Properties[property].Value).OfType<String>().ToList();
                }
                else
                {
                    List<String> s = new List<String>();
                    s.Add((String)adobject.Properties[property].Value);
                    return s;
                }
            }
        }

        public Nullable<Int32> GetInt32(String property)
        {
            if (adobject.Properties[property].Value == null)
            {
                return null;
            }
            else
            {
                return (Int32)adobject.Properties[property].Value;
            }
        }

        public void Close()
        {
            adobject.Close();
        }

        public void Save()
        {
            adobject.CommitChanges();
        }

        /// <summary>
        /// Cleans a string to be suitable for use in LDAP queries
        /// </summary>
        /// <param name="value">an unclean string</param>
        /// <returns>a cleans string</returns>
        protected String CleanLDAPString(String value)
        {
            value.Trim();
            value.Trim(new char[] { (char)0 });
            value.Replace("*", "\\2a");
            value.Replace("(", "\\28");
            value.Replace(")", "\\29");
            value.Replace("\\", "\\5c");
            value.Replace("/", "\\2f");
            value.Replace("" + (char)0, "\\00");
            return value;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    adobject.Dispose();
                }

                // free unmanaged resources (unmanaged objects) and override finalizer

                // set large fields to null
                adobject = null;

                disposedValue = true;
            }
        }

        ~ADObject()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}