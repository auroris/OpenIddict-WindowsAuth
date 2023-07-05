using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Runtime.Versioning;

namespace IdentityServer.ActiveDirectory
{
    [SupportedOSPlatform("windows")]
    public class Printer : ADObject
    {
        public Printer() { }
        public Printer(ADGuid guid) : base(guid) { }
        public Printer(DirectoryEntry entry) : base(entry) { }
        public Printer(String ldap) : base(ldap) { }

        public String PrinterName
        {
            get { return (String)base.adobject.Properties["printerName"].Value; }
        }

        /// <summary>
        /// Gets the object's description
        /// </summary>
        public String Description
        {
            get { return (String)base.adobject.Properties["description"].Value; }
        }

        public String Location
        {
            get { return (String)base.adobject.Properties["location"].Value; }
        }

        public String ServerName
        {
            get { return (String)base.adobject.Properties["serverName"].Value; }
        }

        public String ServerShortName
        {
            get { return (String)base.adobject.Properties["shortServerName"].Value; }
        }

        public String PortName
        {
            get { return (String)base.adobject.Properties["portName"].Value; }
        }

        public String DriverName
        {
            get { return (String)base.adobject.Properties["driverName"].Value; }
        }

        public Nullable<Int32> DriverVersion
        {
            get { return base.GetInt32("driverVersion"); }
        }

        public List<String> BinNames
        {
            get { return base.GetList("printBinNames"); }
        }

        public Nullable<Int32> MaxResolutionSupported
        {
            get { return base.GetInt32("printMaxResolutionSupported"); }
        }

        public List<String> OrientationsSupported
        {
            get { return base.GetList("printOrientationsSupported"); }
        }

        public bool Collate
        {
            get { return (bool)base.adobject.Properties["printCollate"].Value; }
        }

        public bool Color
        {
            get { return (bool)base.adobject.Properties["printColor"].Value; }
        }

        public String Language
        {
            get { return (String)base.adobject.Properties["printLanguage"].Value; }
        }

        public String ShareName
        {
            get { return (String)base.adobject.Properties["printShareName"].Value; }
        }

        public String Spooling
        {
            get { return (String)base.adobject.Properties["printSpooling"].Value; }
        }

        public bool KeepPrintedJobs
        {
            get { return (bool)base.adobject.Properties["printKeepPrintedJobs"].Value; }
        }

        public bool StaplingSupported
        {
            get { return (bool)base.adobject.Properties["printStaplingSupported"].Value; }
        }

        public Nullable<Int32> Memory
        {
            get { return base.GetInt32("printMemory"); }
        }

        public List<String> MediaReady
        {
            get { return base.GetList("printMediaReady"); }
        }

        public List<String> MediaSupported
        {
            get { return base.GetList("printMediaSupported"); }
        }

        public Nullable<Int32> PagesPerMinute
        {
            get { return base.GetInt32("printPagesPerMinute"); }
        }

        public bool DuplexSupported
        {
            get { return (bool)base.adobject.Properties["printDuplexSupported"].Value; }
        }
    }
}