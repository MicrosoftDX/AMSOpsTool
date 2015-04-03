using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.Storage.Blob;

namespace PrototypeConsole
{
    public class IsmSettings
    {
        private static int ERROR_BAD_ARGS = -1,
                           ERROR_CAN_NOT_CONNECT_TO_WAMS = -2,
                           ERROR_BAD_SETTINGS = -3,
                           ERROR_IN_RUNTIME = -4;

        public static int UpdateIsm(string url)//Main(string[] args)
        {
            const string usage = "Usage: ChangeAssetSettings.exe ProgramUrl" +
                                 "\n ChangeAssetSettings.exe http://yourDeployment.origin.mediaservices.windows.net/d357f08b-9ef3-4cf2-b7ea-feb9479f077e/yourFile.ism/Manifest" +
                                 "\nEnsure to setup 'ChangeAssetSettings.exe.config' with your credentials and settings." +
                                 "\nYou can get the Program Url after doing a Program Start or using the Publish URL from the WAMS Portal.";

            ////////////////
            #region Argument checks


            //Get command line args:
            //if (args.Length == 0)
            //{
            //    Console.WriteLine(usage);
            //    return ERROR_BAD_ARGS;
            //}
            //string argument = args[0];

            string argument = url;
            //Validate and qualify args:
            string ismFileName;
            //var assetId = string.Empty;
            string programUrl;
            if (argument.StartsWith("http://"))
            {
                programUrl = argument;
            }
            else
            {
                Console.WriteLine(usage);
                return ERROR_BAD_ARGS;
            }

            //Ensure we will be able to connect to media services:
            CloudMediaContext cloudMediaContext;
            try
            {
                string wamsAccountName = ConfigurationManager.AppSettings["MediaServiceAccountName"];
                string wamsAccountKey = ConfigurationManager.AppSettings["MediaServiceAccountKey"];
                string apiServer = ConfigurationManager.AppSettings["ServiceUrl"];
                string scope = ConfigurationManager.AppSettings["Scope"];
                string acsBaseAddress = ConfigurationManager.AppSettings["ACSAddress"];
                if (!String.IsNullOrEmpty(scope))
                {
                    cloudMediaContext = new CloudMediaContext(new Uri(apiServer), wamsAccountName, wamsAccountKey, scope,
                                                     acsBaseAddress);
                }
                else if (!String.IsNullOrEmpty(apiServer))
                {
                    cloudMediaContext = new CloudMediaContext(new Uri(apiServer), wamsAccountName, wamsAccountKey);
                }
                else
                {
                    cloudMediaContext = new CloudMediaContext(wamsAccountName, wamsAccountKey);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to connect to media services: \n" + e.Message);
                return ERROR_CAN_NOT_CONNECT_TO_WAMS;
            }

            //Ensure configuration is correctly set:
            var fragmentsPerHlsSegmentNew = ConfigurationManager.AppSettings["fragmentsPerHLSSegment"];
            int fragmentsPerHlsSegmentInt32;
            if (!Int32.TryParse(fragmentsPerHlsSegmentNew, out fragmentsPerHlsSegmentInt32) ||
                fragmentsPerHlsSegmentInt32 < 0 ||
                fragmentsPerHlsSegmentInt32 > 5)
            {
                Console.WriteLine("fragmentsPerHLSSegment must be between 1 and 5 in the config file. It is: {0} ",
                                  fragmentsPerHlsSegmentNew);
                return ERROR_BAD_SETTINGS;
            }
            var targetFragmentDurationHnsNew = ConfigurationManager.AppSettings["targetFragmentDurationHNS"];
            int targetFragmentDurationHnsInt32;
            if (!Int32.TryParse(targetFragmentDurationHnsNew, out targetFragmentDurationHnsInt32) ||
                targetFragmentDurationHnsInt32 < 10000000 ||
                targetFragmentDurationHnsInt32 > 100000000)
            {
                Console.WriteLine("targetFragmentDurationHNS must be between 10000000 and 100000000 in the config file. It is: {0} ",
                                  targetFragmentDurationHnsNew);
                return ERROR_BAD_SETTINGS;
            }



            //Parse the arguments to find the WAMS objects we need:
            Guid locatorGuid;
            try
            {
                var programUri = new Uri(programUrl);
                locatorGuid = new Guid(programUri.Segments[1].Trim('/'));

                ismFileName = programUri.Segments[2].Trim('/');
                if (!ismFileName.EndsWith(".ism"))
                {
                    throw new ApplicationException(string.Format("Url does not have 'somefile.ism' as the third URL segment: {0}", ismFileName));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: Failed to parse ProgramUrl: {0} \n {1}", programUrl, e.Message);
                Console.WriteLine(usage);
                return ERROR_BAD_SETTINGS;
            }

            string locatorId = "nb:lid:UUID:" + locatorGuid;

            //If we have locatorId, find the assetId:
            //Use the normal SDK to find the asset and locator:
            ILocator locator = cloudMediaContext.Locators.Where(l => l.Id == locatorId).FirstOrDefault();
            if (locator == null)
            {
                Console.WriteLine("Error: Could not find locator: " + locatorId);
                return ERROR_BAD_SETTINGS;
            }
            IAsset asset = locator.Asset;


            //Check if the user want to overwrite the .ism or use a new name:
            string newIsmFileName = ConfigurationManager.AppSettings["createNewIsmWithName"];
            if (!string.IsNullOrEmpty(newIsmFileName))
            {
                if (!newIsmFileName.EndsWith(".ism"))
                {
                    Console.WriteLine(
                        "Error: The filename provided in the config file for createNewIsmWithName does not end with .ism: {0}", newIsmFileName);
                    return ERROR_BAD_SETTINGS;
                }
            }
            else
            {
                //Overwrite the .ism file:
                newIsmFileName = ismFileName;
            }

            #endregion
            ////////////////


            //Show positive proof of parsing arguments and finding asset:
            Console.WriteLine("Using: Asset.Name ; Asset.Id ; existing ism filname ; new ism filename \n {0} ; {1} ; {2} ; {3}", asset.Name, asset.Id, ismFileName, newIsmFileName);


            //See if we can re-use an existing locator:
            ILocator sasLocator = null;
            String ismSasUri;
            try
            {
                var assetLocators = asset.Locators.ToList();
                foreach (var assetLocator in assetLocators)
                {
                    //It must be SAS
                    if (assetLocator.Type == LocatorType.Sas)
                    {
                        //It must not expire for the next 5 minutes:
                        if (assetLocator.ExpirationDateTime > DateTime.UtcNow.AddMinutes(5))
                        {
                            //It's AccessPolicy must have Read, List, Write permissions:
                            var permissions = assetLocator.AccessPolicy.Permissions;
                            if (permissions.HasFlag(AccessPermissions.List) &&
                                permissions.HasFlag(AccessPermissions.Read) &&
                                permissions.HasFlag(AccessPermissions.Write))
                            {
                                //Use it:
                                sasLocator = assetLocator;
                                break;
                            }
                        }
                    }
                }

                //If we didn't find an existing SAS, create a new one:
                if (sasLocator == null)
                {
                    //Get or create the right access policy.
                    IAccessPolicy accessPolicy = cloudMediaContext.AccessPolicies.Where(ap => ap.Name == "oneDay_ReadListWrite").FirstOrDefault();
                    if (accessPolicy == null)
                    {
                        TimeSpan duration = TimeSpan.FromDays(1);
                        accessPolicy = cloudMediaContext.AccessPolicies.Create("oneDay_ReadListWrite", duration,
                                                                      AccessPermissions.Read | AccessPermissions.List |
                                                                      AccessPermissions.Write);
                    }
                    sasLocator = cloudMediaContext.Locators.CreateLocator(LocatorType.Sas, asset, accessPolicy);
                }

                ismSasUri = sasLocator.BaseUri + "/" + ismFileName + sasLocator.ContentAccessComponent;

                //Show positive proof of having a SAS to use:
                Console.WriteLine("Reading using SAS locator:\n {0}", ismSasUri);

            }
            catch (Exception e)
            {
                Console.WriteLine("Error: Failed to create a SAS Url to download the .ism file.\n{0}", e.Message);
                return ERROR_IN_RUNTIME;

            }

            // //var testIsmSas = "https://nimbuspmteam.blob.core.windows.net/asset-c456ea5d-729a-4edd-b90d-b497921e7d98/2c341e78-5aad-4d19-85e0-067511f9b8f4.ism?sv=2012-02-12&se=2013-09-20T15%3A11%3A32Z&sr=c&si=2255d9a5-0c8c-476d-9538-8030f895ab6f&sig=Vnh4BGEdW5wdpDxnxUqya9pgptTMw9i5tMxuYaIZkiM%3D";

            //Use storage SDK to get the .ism
            var ismXml = new XmlDocument();
            try
            {
                var cbb = new CloudBlockBlob(new Uri(ismSasUri));
                Stream ismXmlStream = new MemoryStream();
                cbb.DownloadToStream(ismXmlStream);

                //Parse it into an XML
                ismXmlStream.Position = 0;
                ismXml.Load(ismXmlStream);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: Failed to retrieve and parse XML of: {0} \n{1}", ismSasUri, e.Message);
                return ERROR_IN_RUNTIME;
            }

            // //var fs = File.OpenText(@"c:\temp\ismFile.txt");
            // //var tempXml = fs.ReadToEnd();
            // //ismXml.LoadXml(tempXml);


            //Parse the xml for the tags to replace and put the modified xml in a new memstream:
            Stream newIsmXmlStream = new MemoryStream();
            try
            {
                var fragmentsPerHlsSegmentOld = string.Empty;
                var targetFragmentDurationHnsOld = string.Empty;

                bool foundfragmentsPerHLSSegment = false;
                bool foundtargetFragmetnDurationHNS = false;

                XmlNodeList ismHeadNodes = ismXml.GetElementsByTagName("meta");
                foreach (XmlNode headNode in ismHeadNodes)
                {
                    if (headNode.Attributes != null)
                    {
                        var nameValue = headNode.Attributes.GetNamedItem("name").Value;
                        if (nameValue == "fragmentsPerHLSSegment")
                        {
                            var fragmentsPerHlsSegmentNode = headNode.Attributes.GetNamedItem("content");
                            fragmentsPerHlsSegmentOld = fragmentsPerHlsSegmentNode.Value;
                            fragmentsPerHlsSegmentNode.Value = fragmentsPerHlsSegmentNew;

                            foundfragmentsPerHLSSegment = true;
                        }
                        if (nameValue == "targetFragmentDurationHNS")
                        {
                            var targetFragmentDurationHnsNode = headNode.Attributes.GetNamedItem("content");
                            targetFragmentDurationHnsOld = targetFragmentDurationHnsNode.Value;
                            targetFragmentDurationHnsNode.Value = targetFragmentDurationHnsNew;

                            foundtargetFragmetnDurationHNS = true;
                        }
                    }
                }

                //if the corresponding meta node does not exist 
                XmlNode objXmlNode = ismXml.DocumentElement.FirstChild;   //<head>
                XmlElement objXmlElement;
                if (!foundfragmentsPerHLSSegment)
                {
                    objXmlElement = ismXml.CreateElement("meta", ismXml.DocumentElement.NamespaceURI);
                    objXmlElement.SetAttribute("name", "fragmentsPerHLSSegment");
                    objXmlElement.SetAttribute("content", ConfigurationManager.AppSettings["fragmentsPerHLSSegment"]);
                    objXmlNode.InsertBefore(objXmlElement, objXmlNode.FirstChild);
                }

                if (!foundtargetFragmetnDurationHNS)
                {
                    objXmlElement = ismXml.CreateElement("meta", ismXml.DocumentElement.NamespaceURI);
                    objXmlElement.SetAttribute("name", "targetFragmentDurationHNS");
                    objXmlElement.SetAttribute("content", ConfigurationManager.AppSettings["targetFragmentDurationHNS"]);
                    objXmlNode.InsertBefore(objXmlElement, objXmlNode.FirstChild);
                }

                //Write this xml into the new memstream:
                ismXml.Save(newIsmXmlStream);

                //Indicate what will be changed to the user:
                Console.WriteLine("Resetting fragmentsPerHLSSegment from {0} to {1} ", fragmentsPerHlsSegmentOld, fragmentsPerHlsSegmentNew);
                Console.WriteLine("Resetting targetFragmentDurationHNS from {0} to {1} ", targetFragmentDurationHnsOld, targetFragmentDurationHnsNew);

            }
            catch (Exception e)
            {
                Console.WriteLine("Error: Failed to parse and replace/add fragmentsPerHLSSegment or targetFragmentDurationHNS.\n{0}", e.Message);
                return ERROR_IN_RUNTIME;
            }


            //By default overwrite .ism file:
            var destinationSas = string.Empty;
            try
            {
                Console.WriteLine("Updating:\n{0}", locator.Path + newIsmFileName + "/Manifest");
                destinationSas = sasLocator.BaseUri + "/" + newIsmFileName + sasLocator.ContentAccessComponent;

                //Write new ism:
                var cbb2 = new CloudBlockBlob(new Uri(destinationSas));
                newIsmXmlStream.Position = 0;
                cbb2.UploadFromStream(newIsmXmlStream);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: Failed to save new .ism file: {0}\n{1}", destinationSas, e.Message);
                return ERROR_IN_RUNTIME;
            }


            return 0;
        }
    }
}
