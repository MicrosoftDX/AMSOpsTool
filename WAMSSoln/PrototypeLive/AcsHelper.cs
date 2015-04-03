using System;
using System.Diagnostics;
using Microsoft.WindowsAzure.MediaServices.Client;

/***********************************************************************
WHAT: Class for getting CloudMediaContext meeting the following requirements:
      [1] Auto-switch ACS endpoint in case of ACS failure (ACS failover)
      [2] Auto-renew ACS token in case of expiration;
      [3] Reuse ACS token;
      [4] Future-proof the code by doing explicit ACS token refresh instead of relying on CloudMediaContext constructor;
      [5] Support ACS instances: primary, replica and Longevity;
      [6] Support multiple instances of CloudMediaContext's to support multiple media services.
      [7] ACS "stickiness": uses and switchtes to the preferred ACS whenever it is available instead of waiting for next failover.
      [8] Even if you have only one ACS, this code can still help you reuse MediaServicesCredentials and auto-renew ACS token.
WHY:  ACS Primary could go down and become single point of failure. For NBC project, we have 2 ACS's: Primary and Replica.
HOW:  To use this class:
            AcsInstance objAcsInstance;
            string mediaServiceName, mediaServiceKey;
            objAcsInstance = AcsInstance.Longevity;
            mediaServiceName = "isp";
            //objAcsInstance = AcsInstance.Replica;
            //mediaServiceName = "nbcsgliveprodeastms";
            //objAcsInstance = AcsInstance.Primary;
            //mediaServiceName = "nbcsgliveprodwestms";
            mediaServiceKey = System.Configuration.ConfigurationManager.AppSettings[mediaServiceName];
            objAcsHelper = new AcsHelper(objAcsInstance, mediaServiceName, mediaServiceKey);
            objCloudMediaContext = objAcsHelper.GetCloudMediaContext();
      If you do not have more than one ACS instance available, just set the following appSettings to false: 
            PreferredACSEnabled
            SwitchACSEnabled
WHEN: 12/2013 
WHO:  willzhan, quintinb
***********************************************************************/
namespace PrototypeLive
{
    public class AcsHelper
    {
        //private instance variables
        private  MediaServicesCredentials objMediaServicesCredentials = null;
        private  object _lockObject = new object();
        
        //properties
        public AcsInstance AcsInstance {get; set;}   //needs to be specified before calling GetCloudMediaContext, otherwise the default (Primary ACS) is used.
        public string MediaServiceName {get; set;}   //need to be specified so that each AcsHelper instance is for one media service
        public string MediaServiceKey  {get; set;}
        
        //constructors
        private AcsHelper() { }  //disable default constructor since parameters are required to instantiate an AcsHelper instance
        public AcsHelper(AcsInstance acsInstance, string mediaServiceName, string mediaServiceKey) 
        {
            this.AcsInstance      = acsInstance;
            this.MediaServiceName = mediaServiceName;
            this.MediaServiceKey  = mediaServiceKey;
        } 

        public CloudMediaContext GetCloudMediaContext()
        {
            System.Diagnostics.Stopwatch objStopwatch = new System.Diagnostics.Stopwatch();
            objStopwatch.Start();

            CloudMediaContext objCloudMediaContext = null;
            objMediaServicesCredentials = GetMediaServicesCredentials();
            Uri uri = GetServiceUri();

            if (uri == null)
            {
                objCloudMediaContext = new CloudMediaContext(objMediaServicesCredentials);
            }
            else
            {
                objCloudMediaContext = new CloudMediaContext(uri, objMediaServicesCredentials);
            }
            Console.WriteLine("Time taken = {0}(ms), ACS Instance = {1}, TokenExpiration = {2}, AccessToken={3}", objStopwatch.ElapsedMilliseconds.ToString(), this.AcsInstance.ToString(), objCloudMediaContext.Credentials.TokenExpiration.ToString("MM/dd/yyyy HH:mm:ss"), objCloudMediaContext.Credentials.AccessToken);
            
            return objCloudMediaContext;
        }

        private void SwitchAcsEndpoint()
        {
            if (this.IsSwitchAcsEnabled())
            {
                switch (this.AcsInstance)
                {
                    case AcsInstance.Preferred:
                    case AcsInstance.Replica:
                        Logger.Log(string.Format("ACS instance is switched from {0} to Primary.", this.AcsInstance.ToString()), EventLogEntryType.Information);
                        Console.WriteLine("ACS instance is switched from {0} to Primary.", this.AcsInstance.ToString());
                        this.AcsInstance = AcsInstance.Primary;
                        break;
                    case AcsInstance.Primary:
                        Logger.Log(string.Format("ACS instance is switched from {0} to Replica.", this.AcsInstance.ToString()), EventLogEntryType.Information);
                        Console.WriteLine("ACS instance is switched from {0} to Replica.", this.AcsInstance.ToString());
                        this.AcsInstance = AcsInstance.Replica;
                        break;
                    case AcsInstance.Longevity:
                    case AcsInstance.Staging:
                        //No ACS redundancy for Longevity or Staging
                        break;
                    default:
                        break;
                }
            }
        }

        private bool IsAcsTokenExpired(MediaServicesCredentials objMediaServicesCredentials)
        {
            //MediaServicesCredentials.TokenExpiration is in UTC. Renew it 10 min ahead.
            return (objMediaServicesCredentials.TokenExpiration < DateTime.UtcNow.AddMinutes(10));
        }

        private Uri GetServiceUri()
        {
            string serviceUrl = string.Empty;
            switch (this.AcsInstance)
            {
                case AcsInstance.Replica:
                    serviceUrl = System.Configuration.ConfigurationManager.AppSettings["ServiceUrl_Replica"];
                    if (string.IsNullOrEmpty(serviceUrl))
                        serviceUrl = AcsConfig.SERVICE_URL_REPLICA;
                    break;
                case AcsInstance.Primary:
                    serviceUrl = System.Configuration.ConfigurationManager.AppSettings["ServiceUrl_Primary"];
                    if (string.IsNullOrEmpty(serviceUrl))
                        serviceUrl = AcsConfig.SERVICE_URL_PRIMARY;
                    break;
                case AcsInstance.Preferred:
                    serviceUrl = System.Configuration.ConfigurationManager.AppSettings["ServiceUrl_Preferred"];
                    if (string.IsNullOrEmpty(serviceUrl))
                        serviceUrl = AcsConfig.SERVICE_URL_REPLICA;
                    break;
                case AcsInstance.Longevity:
                    serviceUrl = System.Configuration.ConfigurationManager.AppSettings["ServiceUrl_Longevity"];
                    if (string.IsNullOrEmpty(serviceUrl))
                        serviceUrl = AcsConfig.SERVICE_URL_LONGEVITY;
                    break;
                case AcsInstance.Staging:
                    serviceUrl = System.Configuration.ConfigurationManager.AppSettings["ServiceUrl_Staging"];
                    if (string.IsNullOrEmpty(serviceUrl))
                        serviceUrl = AcsConfig.SERVICE_URL_STAGING;
                    break;
                default:
                    break;
            }
            if (string.IsNullOrEmpty(serviceUrl))
                return null;
            else
            {
                return new Uri(serviceUrl);
            }
        }

        //when the application process starts and the very first MediaServicesCredentials is created, there is no ACS access:
        //MediaServicesCredentials.AccessToken = null;
        //MediaServicesCredentials.TokenExpiration = {1/1/0001 12:00:00 AM}; (obviously expired)
        //MediaServices.RefreshToken() will get the very first token from ACS.
        private MediaServicesCredentials CreateMediaServicesCredentials()
        {
            string acsAddress;
            string scope;
            MediaServicesCredentials objMediaServicesCredentials1 = null;

            switch (this.AcsInstance)
            {
                case AcsInstance.Replica:
                    acsAddress  = System.Configuration.ConfigurationManager.AppSettings["ACSAddress_Replica"];
                    scope       = System.Configuration.ConfigurationManager.AppSettings["Scope_Replica"];
                    if (string.IsNullOrEmpty(acsAddress))
                        acsAddress = AcsConfig.ACS_ADDRESS_REPLICA;
                    if (string.IsNullOrEmpty(scope))
                        scope = AcsConfig.SCOPE_REPLICA;  
                    break;
                case AcsInstance.Primary:
                    acsAddress  = System.Configuration.ConfigurationManager.AppSettings["ACSAddress_Primary"];
                    scope       = System.Configuration.ConfigurationManager.AppSettings["Scope_Primary"];
                    if (string.IsNullOrEmpty(acsAddress))
                        acsAddress = AcsConfig.ACS_ADDRESS_PRIMARY;
                    if (string.IsNullOrEmpty(scope))
                        scope = AcsConfig.SCOPE_PRIMARY;
                    break;
                case AcsInstance.Preferred:
                    acsAddress  = System.Configuration.ConfigurationManager.AppSettings["ACSAddress_Preferred"];
                    scope       = System.Configuration.ConfigurationManager.AppSettings["Scope_Preferred"];
                    if (string.IsNullOrEmpty(acsAddress))
                        acsAddress = AcsConfig.ACS_ADDRESS_REPLICA;
                    if (string.IsNullOrEmpty(scope))
                        scope = AcsConfig.SCOPE_REPLICA;
                    break;
                case AcsInstance.Longevity:
                    //no ACS redundancy for Longevity
                    acsAddress  = System.Configuration.ConfigurationManager.AppSettings["ACSAddress_Longevity"];
                    scope       = System.Configuration.ConfigurationManager.AppSettings["Scope_Longevity"];
                    if (string.IsNullOrEmpty(acsAddress))
                        acsAddress = AcsConfig.ACS_ADDRESS_LONGEVITY;
                    if (string.IsNullOrEmpty(scope))
                        scope = AcsConfig.SCOPE_LONGEVITY;
                    break;
                case AcsInstance.Staging:
                    //no ACS redundancy for Staging
                    acsAddress = System.Configuration.ConfigurationManager.AppSettings["ACSAddress_Staging"];
                    scope = System.Configuration.ConfigurationManager.AppSettings["Scope_Staging"];
                    if (string.IsNullOrEmpty(acsAddress))
                        acsAddress = AcsConfig.ACS_ADDRESS_STAGING;
                    if (string.IsNullOrEmpty(scope))
                        scope = AcsConfig.SCOPE_STAGING;
                    break;
                default:
                    acsAddress  = System.Configuration.ConfigurationManager.AppSettings["ACSAddress_Primary"];
                    scope       = System.Configuration.ConfigurationManager.AppSettings["Scope_Primary"];
                    if (string.IsNullOrEmpty(acsAddress))
                        acsAddress = AcsConfig.ACS_ADDRESS_PRIMARY;
                    if (string.IsNullOrEmpty(scope))
                        scope = AcsConfig.SCOPE_PRIMARY;
                    break;
            }
            objMediaServicesCredentials1 = new MediaServicesCredentials(this.MediaServiceName, this.MediaServiceKey, scope, acsAddress);

            //use an old ACS token to test token renewal by setting TokenExpiration to next 2 min
            bool inTestMode = false;
            if (inTestMode)
            {
                //ACS token obtained on 12/20/2013, 18:10:00 GMT. Media service: nbcsgliveprodwestms
                objMediaServicesCredentials1.AccessToken     = "http%3a%2f%2fschemas.xmlsoap.org%2fws%2f2005%2f05%2fidentity%2fclaims%2fnameidentifier=nbcsgliveprodwestms&urn%3aSubscriptionId=5f611124-e90f-488d-a187-fbe2fae12612&http%3a%2f%2fschemas.microsoft.com%2faccesscontrolservice%2f2010%2f07%2fclaims%2fidentityprovider=https%3a%2f%2fwamsprodglobal002acs.accesscontrol.windows.net%2f&Audience=urn%3aWindowsAzureMediaServices&ExpiresOn=1387584476&Issuer=https%3a%2f%2fwamsprodglobal002acs.accesscontrol.windows.net%2f&HMACSHA256=ac9gxHi2IuwXaj858b3rQ6FiMo2BKKVP%2fvJPdtVMQps%3d";  
                objMediaServicesCredentials1.TokenExpiration = DateTime.UtcNow.AddMinutes(2.0);
                Console.WriteLine("A really old, hard-coded ACS token is being used.");
            }

            return objMediaServicesCredentials1;
        }

        private MediaServicesCredentials GetMediaServicesCredentials()
        {
            lock (_lockObject)
            {
                // If the credentials object is null, create one
                if (objMediaServicesCredentials == null)
                {
                    objMediaServicesCredentials = this.CreateMediaServicesCredentials();
                    Console.WriteLine("A new MediaServicesCredentials is created.");
                }
                else
                {
                    Console.WriteLine("Reusing the existing MediaServicesCredentials.");
                }

                // If the credentials object has an expired (or null) token, refresh the token via ACS authentication
                if (IsAcsTokenExpired(objMediaServicesCredentials))
                {
                    this.RefreshAcsToken();
                }

                return objMediaServicesCredentials;
            } 
        }

        private void RefreshAcsToken()
        {
            bool exit = false;
            int timesToSwitchEndpointAndRetry = 1;

            //first try preferred ACS
            if (this.IsPreferredAcsEnabled() && (this.AcsInstance != AcsInstance.Longevity))
            {
                if (!this.IsPreferredAcsEndpoint(objMediaServicesCredentials))
                {
                    this.SwitchAcsEndpointToPreferred();
                    objMediaServicesCredentials = this.CreateMediaServicesCredentials();
                }
            }

            while (!exit)
            {
                try
                {
                    bool isNullAcsToken = (objMediaServicesCredentials.AccessToken == null);
                    objMediaServicesCredentials.RefreshToken();  //ACS Token is constructed if not yet, by authenticating against ACS
                    exit = true;
                    if (isNullAcsToken)
                        Console.WriteLine("A new ACS token has been created.");
                    else
                        Console.WriteLine("The ACS token has been renewed.");
                }
                catch (Exception)
                {
                    // we hit an error, switch the endpoint to try so that the next attempt will be on a different endpoint
                    SwitchAcsEndpoint();

                    timesToSwitchEndpointAndRetry--;
                    if (timesToSwitchEndpointAndRetry < 0)
                    {
                        Logger.Log("Have tried all available ACS instances and failed.", EventLogEntryType.Error);
                        throw new Exception("Have tried all available ACS instances and failed.");
                    }

                    // Create the new credentials instance with the next endpoint for the retry
                    objMediaServicesCredentials = this.CreateMediaServicesCredentials();
                }
            }
        }

        private bool IsPreferredAcsEndpoint(MediaServicesCredentials objMediaServicesCredentials1)
        {
            string preferredAcsEndpoint = System.Configuration.ConfigurationManager.AppSettings["ACSAddress_Preferred"];
            if (string.IsNullOrEmpty(preferredAcsEndpoint))
                preferredAcsEndpoint = AcsConfig.ACS_ADDRESS_REPLICA;   //assuming ACS Replica is the preferred ACS

            return (objMediaServicesCredentials1.AcsBaseAddress == preferredAcsEndpoint);
        }

        private void SwitchAcsEndpointToPreferred()
        {
            switch (this.AcsInstance)
            {
                case AcsInstance.Replica:
                case AcsInstance.Primary:
                    Logger.Log(string.Format("Switching from {0} to Preferred ACS", this.AcsInstance.ToString()), EventLogEntryType.Information);
                    Console.WriteLine("Switching from {0} to Preferred ACS", this.AcsInstance.ToString());
                    this.AcsInstance = AcsInstance.Preferred;
                    break;
                case AcsInstance.Longevity:
                case AcsInstance.Staging:
                    //No ACS redundancy, no preferred ACS for Longevity or Staging
                    break;
                default:
                    break;
            }
        }

        private bool IsSwitchAcsEnabled()
        {
            bool switchAcsEnabled = AcsConfig.SWITCH_ACS_ENABLED;
            string switchAcsConfig = System.Configuration.ConfigurationManager.AppSettings["SwitchACSEnabled"];
            if (!string.IsNullOrEmpty(switchAcsConfig))
                switchAcsEnabled = bool.Parse(switchAcsConfig);

            return switchAcsEnabled;
        }

        private bool IsPreferredAcsEnabled()
        {
            bool preferredAcsEnabled = AcsConfig.PREFERRED_ACS_ENABLED;
            string preferredAcsConfig = System.Configuration.ConfigurationManager.AppSettings["PreferredACSEnabled"];
            if (!string.IsNullOrEmpty(preferredAcsConfig))
                preferredAcsEnabled = bool.Parse(preferredAcsConfig);

            return preferredAcsEnabled;
        }

    }  //class AcsHelper


    public enum AcsInstance
    {
        Primary,   //general production ACS
        Replica,   //Replica ACS, NBC project will use
        Preferred, //should be the same as Replica for NBC
        Longevity, //for test only
        Staging    //for Staging
    }

    public class AcsConfig
    {
        public const string SERVICE_URL_PRIMARY   = "https://media.windows.net";
        public const string ACS_ADDRESS_PRIMARY   = "https://wamsprodglobal001acs.accesscontrol.windows.net/";
        public const string SCOPE_PRIMARY         = "urn:WindowsAzureMediaServices";

        public const string SERVICE_URL_REPLICA   = "https://media.windows.net";
        public const string ACS_ADDRESS_REPLICA   = "https://wamsprodglobal002acs.accesscontrol.windows.net/";
        public const string SCOPE_REPLICA         = "urn:WindowsAzureMediaServices";

        public const string SERVICE_URL_LONGEVITY = "https://originlongevitytestus.cloudapp.net/API/";
        public const string ACS_ADDRESS_LONGEVITY = "https://nimbustestaccounts.accesscontrol.windows.net/";
        public const string SCOPE_LONGEVITY       = "urn:Nimbus";

        public const string SERVICE_URL_STAGING   = "https://wamsintclus001rest-hs.cloudapp.net/API/";
        public const string ACS_ADDRESS_STAGING   = "https://nimbusintacs.accesscontrol.windows.net";
        public const string SCOPE_STAGING         = "urn:windowsazuremediaservices";

        public const bool SWITCH_ACS_ENABLED      = true;
        public const bool PREFERRED_ACS_ENABLED   = true;
    }

    //for simple Event log logging
    public class Logger
    {
        private static string LOG = "Application";
        private static string SOURCE = "AcsHelper.cs";

        public static void Log(string msg, EventLogEntryType objEventLogEntryType)
        {
            if (!EventLog.SourceExists(SOURCE))
                EventLog.CreateEventSource(SOURCE, LOG);

            try
            {
                EventLog.WriteEntry(SOURCE, msg, objEventLogEntryType);
            }
            catch { }
        }
    }

} //namespace
