using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static ActiveDirectory.ADObject;

namespace ActiveDirectory
{
    /// <summary>
    /// A fluent builder for <see cref="DirectorySearcher"/> queries.
    /// Construct with an optional LDAP root and an <see cref="ObjectClass"/>,
    /// add attribute/value pairs to <see cref="PropertiesToSearch"/> (all OR'd together),
    /// then call <see cref="FindAll{T}"/> or <see cref="FindOne{T}"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// var search = new DirectorySearch(ldapRoot, ObjectClass.User);
    /// search.PropertiesToSearch.Add("displayName", query, DirectorySearch.MatchType.StartsWith);
    /// search.PropertiesToSearch.Add("givenName",   query, DirectorySearch.MatchType.StartsWith);
    /// search.PropertiesToLoad.Add("displayName");
    /// List&lt;User&gt; users = search.FindAll&lt;User&gt;();
    /// </code>
    /// </example>
    public class DirectorySearch
    {
        private static ILogger? _log;
        private static ILogger Log => _log ??=
            (IdentityServer.Program.LoggerFactory ?? NullLoggerFactory.Instance)
            .CreateLogger<DirectorySearch>();

        /// <summary>Controls how a filter value is matched against an attribute.</summary>
        public enum MatchType
        {
            /// <summary>The attribute must exactly equal the value: <c>(attr=value)</c>.</summary>
            Exact,
            /// <summary>The attribute must start with the value: <c>(attr=value*)</c>.</summary>
            StartsWith,
            /// <summary>
            /// The attribute must contain the value anywhere: <c>(attr=*value*)</c>.
            /// Note: leading-wildcard searches are not indexed and may be slow on large directories.
            /// </summary>
            Contains
        }

        private readonly string? _ldapRoot;
        private readonly ObjectClass _objectClass;

        /// <summary>
        /// Attribute/value filter pairs to OR together in the search filter.
        /// All entries are combined with a logical OR; the <see cref="coldlake.mil.ca.ActiveDirectory.ObjectClass"/> filter
        /// is always AND'd around the result.
        /// </summary>
        public SearchFilterCollection PropertiesToSearch { get; } = new();

        /// <summary>
        /// Attribute names to pre-fetch from the directory. When non-empty, only the listed
        /// attributes are retrieved in the initial query; others are lazy-loaded on demand.
        /// </summary>
        public List<string> PropertiesToLoad { get; } = new();

        /// <summary>
        /// An additional raw LDAP filter assertion AND'd with the object-class filter and any
        /// <see cref="PropertiesToSearch"/> entries (e.g. <c>memberOf=&lt;GUID=...&gt;</c>).
        /// The value must already be a valid, fully-formed LDAP assertion; it is NOT sanitised
        /// by <see cref="LdapHelper.CleanLDAPString"/>. Do not use for untrusted user input —
        /// use <see cref="PropertiesToSearch"/> for that.
        /// </summary>
        public string? AdditionalFilter { get; set; }

        /// <summary>Creates a search scoped to the entire domain.</summary>
        /// <param name="objectClass">The type of AD object to search for.</param>
        public DirectorySearch(ObjectClass objectClass) : this(null, objectClass) { }

        /// <summary>Creates a search rooted at a specific LDAP path.</summary>
        /// <param name="ldapRoot">
        /// A full LDAP path (e.g. <c>LDAP://OU=Users,DC=example,DC=com</c>),
        /// or <c>null</c> to use the domain default.
        /// </param>
        /// <param name="objectClass">The type of AD object to search for.</param>
        public DirectorySearch(string? ldapRoot, ObjectClass objectClass)
        {
            _ldapRoot = ldapRoot;
            _objectClass = objectClass;
        }

        private string BuildFilter()
        {
            string classFilter = _objectClass switch
            {
                ObjectClass.User     => "objectClass=user",
                ObjectClass.Group    => "objectClass=group",
                ObjectClass.Computer => "objectClass=computer",
                ObjectClass.Printer  => "objectClass=printQueue",
                _                    => "objectClass=*"
            };

            var andTerms = new List<string> { $"({classFilter})" };

            if (AdditionalFilter != null)
                andTerms.Add($"({AdditionalFilter})");

            if (PropertiesToSearch.Count > 0)
            {
                string attrParts = string.Concat(PropertiesToSearch.Items.Select(f =>
                {
                    string v = LdapHelper.CleanLDAPString(f.Value);
                    return f.Match switch
                    {
                        MatchType.Contains   => $"({f.Attribute}=*{v}*)",
                        MatchType.StartsWith => $"({f.Attribute}={v}*)",
                        _                    => $"({f.Attribute}={v})"
                    };
                }));
                andTerms.Add(PropertiesToSearch.Count == 1 ? attrParts : $"(|{attrParts})");
            }

            return andTerms.Count == 1 ? andTerms[0] : $"(&{string.Concat(andTerms)})";
        }

        /// <summary>
        /// Number of results to retrieve per server round-trip when using paging.
        /// Set to a positive value (e.g. <c>1000</c>) to enable server-side paging, which is
        /// required to reliably retrieve more than the server's default result limit (typically 1000).
        /// Defaults to <c>0</c> (no paging).
        /// </summary>
        public int PageSize { get; set; } = 0;

        private DirectorySearcher CreateSearcher(DirectoryEntry? root)
        {
            var searcher = root != null
                ? new DirectorySearcher(root, BuildFilter())
                : new DirectorySearcher(BuildFilter());

            foreach (string prop in PropertiesToLoad)
                searcher.PropertiesToLoad.Add(prop);

            // Always include objectguid so EnsureEntry() can use stable GUID-based binding.
            if (!PropertiesToLoad.Contains(ADProperties.ObjectGuid))
                searcher.PropertiesToLoad.Add(ADProperties.ObjectGuid);

            if (PageSize > 0)
                searcher.PageSize = PageSize;

            return searcher;
        }

        /// <summary>
        /// Executes the search and returns matching objects as <typeparamref name="T"/>.
        /// Each returned object wraps a live <see cref="System.DirectoryServices.DirectoryEntry"/>
        /// and should be disposed when no longer needed.
        /// </summary>
        /// <param name="howMany">
        /// Maximum number of results to return. Pass <c>null</c> (the default) to return all matches.
        /// Sets <see cref="DirectorySearcher.SizeLimit"/> on the underlying searcher, so the limit
        /// is enforced server-side.
        /// </param>
        public List<T> Find<T>(int? howMany = null) where T : ADObject, new()
        {
            var list = new List<T>();
            DirectoryEntry? searchRoot = _ldapRoot != null ? new DirectoryEntry(_ldapRoot) : null;
            try
            {
                using var searcher = CreateSearcher(searchRoot);
                if (howMany.HasValue)
                    searcher.SizeLimit = howMany.Value;
                using var results = searcher.FindAll();
                foreach (SearchResult result in results)
                {
                    var obj = new T();
                    obj.SetFromResult(result);
                    list.Add(obj);
                }
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "AD search failed (filter: {Filter}, root: {Root})",
                    BuildFilter(), _ldapRoot ?? "(domain default)");
                throw;
            }
            finally
            {
                searchRoot?.Dispose();
            }
            return list;
        }

        /// <summary>
        /// Executes the search and returns the <see cref="DirectoryEntry"/> of the first matching
        /// object, or <c>null</c> if no match is found. The caller takes ownership of the returned
        /// entry and is responsible for disposing it.
        /// </summary>
        public DirectoryEntry? FindOneEntry()
        {
            DirectoryEntry? searchRoot = _ldapRoot != null ? new DirectoryEntry(_ldapRoot) : null;
            try
            {
                using var searcher = CreateSearcher(searchRoot);
                SearchResult? result = searcher.FindOne();
                return result?.GetDirectoryEntry();
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "AD FindOneEntry failed (filter: {Filter}, root: {Root})",
                    BuildFilter(), _ldapRoot ?? "(domain default)");
                throw;
            }
            finally
            {
                searchRoot?.Dispose();
            }
        }

        /// <summary>
        /// Executes the search and returns the first matching object as <typeparamref name="T"/>,
        /// or <c>null</c> if no match is found.
        /// The returned object should be disposed when no longer needed.
        /// </summary>
        public T? FindOne<T>() where T : ADObject, new()
        {
            DirectoryEntry? searchRoot = _ldapRoot != null ? new DirectoryEntry(_ldapRoot) : null;
            try
            {
                using var searcher = CreateSearcher(searchRoot);
                SearchResult? result = searcher.FindOne();
                if (result == null) return null;
                var obj = new T();
                obj.SetFromResult(result);
                return obj;
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "AD FindOne failed (filter: {Filter}, root: {Root})",
                    BuildFilter(), _ldapRoot ?? "(domain default)");
                throw;
            }
            finally
            {
                searchRoot?.Dispose();
            }
        }

        /// <summary>
        /// A collection of attribute/value/match-type filter entries used by
        /// <see cref="PropertiesToSearch"/>. All entries are OR'd together in the search filter.
        /// </summary>
        public class SearchFilterCollection
        {
            private readonly List<SearchFilter> _items = new();

            /// <summary>Adds an exact-match filter: <c>(attribute=value)</c>.</summary>
            public void Add(string attribute, string value) =>
                _items.Add(new SearchFilter(attribute, value, MatchType.Exact));

            /// <summary>Adds a filter with the specified <paramref name="matchType"/>.</summary>
            public void Add(string attribute, string value, MatchType matchType) =>
                _items.Add(new SearchFilter(attribute, value, matchType));

            internal IEnumerable<SearchFilter> Items => _items;
            internal int Count => _items.Count;
        }

        internal record SearchFilter(string Attribute, string Value, MatchType Match);
    }
}
