﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.MediaServices.Client.ContentKeyAuthorization;
using Microsoft.WindowsAzure.MediaServices.Client.DynamicEncryption;
using System.Security.Cryptography;
using System.Net;
using System.Collections.Specialized;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Xml.XPath;
using System.Security.Cryptography.X509Certificates;


namespace PrototypeConsole
{
    public class CryptoUtils
    {
        //Live protection: given channel name and program name, set up dynamic PlayReady protection for live and start the program
        public static void SetupLiveDynamicPlayReadyProtection(CloudMediaContext objCloudMediaContext, string channelName, string programName, string manifestFileName)
        {
            //get channel
            IChannel objIChannel = objCloudMediaContext.Channels.Where(c => c.Name == channelName).FirstOrDefault();
            Console.WriteLine(string.Format("IChannel.IngestUrl = {0}",  objIChannel.Input.Endpoints.FirstOrDefault().Url.ToString()));
            Console.WriteLine(string.Format("IChannel.PreviewUrl = {0}", objIChannel.Preview.Endpoints.FirstOrDefault().Url.ToString()));

            //create program asset
            IAsset objIAsset = objCloudMediaContext.Assets.Create(string.Format("{0}_Program_Asset_PR", channelName), AssetCreationOptions.None);

            //set up dynamic PlayReady protection for the asset exactly as VOD
            SetupDynamicPlayReadyProtection(objCloudMediaContext, objIAsset);

            //create a program using this asset 
            ProgramCreationOptions options = new ProgramCreationOptions()
            {
                Name                = programName,
                Description         = "Dynamic PlayReady protection for live",
                ArchiveWindowLength = TimeSpan.FromMinutes(120.0),
                ManifestName        = manifestFileName, //manifest file name to be duplicated (without .ism suffix)
                AssetId             = objIAsset.Id
            };
            IProgram objIProgram = objIChannel.Programs.Create(options);

            //publish the asset
            Program.GetStreamingOriginLocator(objIAsset.Id, Program.MediaContentType.SmoothStreaming, true);

            //start the program
            objIProgram.Start();
            Console.WriteLine("Program {0} has started", programName);
        }

        //given an unprotected IAsset, set up dynamic PR protection
        //you have to publish AFTER this setup
        public static void SetupDynamicPlayReadyProtection(CloudMediaContext objCloudMediaContext, IAsset objIAsset)
        {
            string keySeedB64, contentKeyB64;
            Guid keyId = Guid.NewGuid();
            //Different ways to create content key:
            //Method 1: Without using key seed, generete content key directly
            //contentKeyB64 = GeneratePlayReadyContentKey();
            //Method 2: With a given key seed and generated key ID (Key Identifiers are unique in the system and there can only be one key with a given Guid within a cluster (even across accounts for now although that may change to be account scoped in the future).  If you try to submit a protection job with a keyId that already exists but a different key value that will cause the PlayReady protection job to fail (the same keyId and keyValue is okay). 
            keySeedB64 = "XVBovsmzhP9gRIZxWfFta3VVRPzVEWmJsazEJ46I";
            contentKeyB64 = GetPlayReadyContentKeyFromKeyIdKeySeed(keyId.ToString(), keySeedB64);
            //Method 3: With a randomly generated key seed, create content key from the key ID and key seed
            //keySeedB64 = GeneratePlayReadyKeySeed();
            //contentKeyB64 = GetPlayReadyContentKeyFromKeyIdKeySeed(keyId.ToString(), keySeedB64);
            //Method 4: Reuse an existing key ID
            //keyId = new Guid("a7586184-40ff-4047-9edd-6a8273ac50fc");
            //keySeedB64 = "XVBovsmzhP9gRIZxWfFta3VVRPzVEWmJsazEJ46I";
            //contentKeyB64 = GetPlayReadyContentKeyFromKeyIdKeySeed(keyId.ToString(), keySeedB64);
            Console.WriteLine(string.Format("STEP 1: Key ID = {0}, Content Key = {1}, Key Seed = {2}", contentKeyB64, keyId.ToString(), keySeedB64));

            IContentKey objIContentKey = ConfigureKeyDeliveryService(objCloudMediaContext, keyId, contentKeyB64, objIAsset.Id);

            //associate IAsset with the IContentkey
            objIAsset.ContentKeys.Add(objIContentKey);
            CreateAssetDeliveryPolicy(objCloudMediaContext, objIAsset, objIContentKey);
        }

        public static void SetupDynamicPlayReadyProtectionWithExistingContentKey(CloudMediaContext objCloudMediaContext, IAsset objIAsset)
        {
            string contentKeyId = "nb:kid:UUID:d74192ae-d85a-4dbd-a5b0-7ba418c19db6";   //UnprotectedSmoothStreamingAssetLync7
            IContentKey objIContentKey = objCloudMediaContext.ContentKeys.Where(k => k.Id == contentKeyId).FirstOrDefault();

            //associate IAsset with the IContentkey
            objIAsset.ContentKeys.Add(objIContentKey);
            CreateAssetDeliveryPolicy(objCloudMediaContext, objIAsset, objIContentKey);
        }

        public static void SetupContentKeyWithoutProtection(CloudMediaContext objCloudMediaContext, IAsset objIAsset)
        {
            string keySeedB64, contentKeyB64;
            Guid keyId = Guid.NewGuid();

            //Method 2: With a given key seed and generated key ID (Key Identifiers are unique in the system and there can only be one key with a given Guid within a cluster (even across accounts for now although that may change to be account scoped in the future).  If you try to submit a protection job with a keyId that already exists but a different key value that will cause the PlayReady protection job to fail (the same keyId and keyValue is okay). 
            keySeedB64 = "XVBovsmzhP9gRIZxWfFta3VVRPzVEWmJsazEJ46I";
            contentKeyB64 = GetPlayReadyContentKeyFromKeyIdKeySeed(keyId.ToString(), keySeedB64);
            Console.WriteLine(string.Format("STEP 1: Key ID = {0}, Content Key = {1}, Key Seed = {2}", contentKeyB64, keyId.ToString(), keySeedB64));

            IContentKey objIContentKey = ConfigureKeyDeliveryService_Unprotected(objCloudMediaContext, keyId, contentKeyB64);

            //associate IAsset with the IContentkey
            objIAsset.ContentKeys.Add(objIContentKey);
            CreateAssetDeliveryPolicy_Unprotected(objCloudMediaContext, objIAsset, objIContentKey);
        }

        //configure dynamic PlayReady protection of an IAsset using an IContentkey
        static public void CreateAssetDeliveryPolicy(CloudMediaContext objCloudMediaContext, IAsset objIAsset, IContentKey objIContentKey)
        {
            Uri acquisitionUrl = objIContentKey.GetKeyDeliveryUrl(ContentKeyDeliveryType.PlayReadyLicense);

            Dictionary<AssetDeliveryPolicyConfigurationKey, string> assetDeliveryPolicyConfiguration = new Dictionary<AssetDeliveryPolicyConfigurationKey, string>
            {
                {AssetDeliveryPolicyConfigurationKey.PlayReadyLicenseAcquisitionUrl, acquisitionUrl.ToString()},
            };

            var assetDeliveryPolicy = objCloudMediaContext.AssetDeliveryPolicies.Create(
                    "AssetDeliveryPolicy",
                    AssetDeliveryPolicyType.DynamicCommonEncryption,
                    AssetDeliveryProtocol.SmoothStreaming | AssetDeliveryProtocol.Dash,
                    assetDeliveryPolicyConfiguration);

            // Add AssetDelivery Policy to the asset
            objIAsset.DeliveryPolicies.Add(assetDeliveryPolicy);

            Console.WriteLine("Adding Asset Delivery Policy: " + assetDeliveryPolicy.AssetDeliveryPolicyType);
        }

        static public void CreateAssetDeliveryPolicy_Unprotected(CloudMediaContext objCloudMediaContext, IAsset objIAsset, IContentKey objIContentKey)
        {
            var assetDeliveryPolicy = objCloudMediaContext.AssetDeliveryPolicies.Create(
                    "AssetDeliveryPolicy",
                    AssetDeliveryPolicyType.NoDynamicEncryption,
                    AssetDeliveryProtocol.Dash,
                    null);

            // Add AssetDelivery Policy to the asset
            objIAsset.DeliveryPolicies.Add(assetDeliveryPolicy);

            Console.WriteLine("Adding Asset Delivery Policy: " + assetDeliveryPolicy.AssetDeliveryPolicyType);
        }

        public static void DoVodDrmFlow(CloudMediaContext objCloudMediaContext)
        {
            //Step 1: Create key ID and content key
            string keySeedB64, contentKeyB64;
            Guid keyId = Guid.NewGuid();
            //Guid keyId = new Guid("09a2212a-a803-4989-9a6e-6cd2e69500e7");   
            //Method 1: Without using key seed, generete content key directly
            //contentKeyB64 = GeneratePlayReadyContentKey();
            //Method 2: With a given key seed and generated key ID (Key Identifiers are unique in the system and there can only be one key with a given Guid within a cluster (even across accounts for now although that may change to be account scoped in the future).  If you try to submit a protection job with a keyId that already exists but a different key value that will cause the PlayReady protection job to fail (the same keyId and keyValue is okay). 
            keySeedB64 = "XVBovsmzhP9gRIZxWfFta3VVRPzVEWmJsazEJ46I";
            contentKeyB64 = GetPlayReadyContentKeyFromKeyIdKeySeed(keyId.ToString(), keySeedB64);
            //Method 3: With a randomly generated key seed, create content key from the key ID and key seed
            //keySeedB64 = GeneratePlayReadyKeySeed();
            //contentKeyB64 = GetPlayReadyContentKeyFromKeyIdKeySeed(keyId.ToString(), keySeedB64);
            //Method 4: Reuse an existing key ID
            //keyId = new Guid("a7586184-40ff-4047-9edd-6a8273ac50fc");
            //keySeedB64 = "XVBovsmzhP9gRIZxWfFta3VVRPzVEWmJsazEJ46I";
            //contentKeyB64 = GetPlayReadyContentKeyFromKeyIdKeySeed(keyId.ToString(), keySeedB64);
            Console.WriteLine(string.Format("STEP 1: Key ID = {0}, Content Key = {1}, Key Seed = {2}", contentKeyB64, keyId.ToString(), keySeedB64));

            //Step 2: Update PlayReady protection configuration XML file
            Console.WriteLine("STEP 2: Update configuration XML file");
            UpdatePlayReadyConfigurationXMLFile(keyId, contentKeyB64);

            //Step 3: Transcode job
            //Console.WriteLine("STEP 4: PlayReady protection workflow");
            //string path = @"C:\Workspace\Destination\Input\MultipleFile\MP4";
            ////string path = @"C:\Workspace\Destination\Input\MultipleFile\Silverlight";
            //string configFilePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\..\..";
            //Program.CreatePlayReadyProtectionJob(path, configFilePath);
            string path = @"C:\Workspace\Destination\Input\SingileFile\BigBuckBunny.mp4";
            IAsset objIAsset = Program.CreateAssetAndUploadSingleFile(AssetCreationOptions.None, path);
            string outputFolder = @"C:\Workspace\Destination\Output";
            IAsset objIAssetOut;
            IJob objIJob = Program.DoWorkflow(objIAsset, outputFolder, out objIAssetOut);

            //Step 4: Configure key delivery service
            Console.WriteLine("STEP 3: Configure key delivery service");
            ConfigureKeyDeliveryService(objCloudMediaContext, keyId, contentKeyB64, objIAsset.Id);
        }
        //http://msdn.microsoft.com/en-us/library/dn189154.aspx
        public static void UpdatePlayReadyConfigurationXMLFile(Guid keyId, string contentKeyB64)
        {
            System.Xml.Linq.XNamespace xmlns = "http://schemas.microsoft.com/iis/media/v4/TM/TaskDefinition#";
            string keyDeliveryServiceUriStr = "http://playready.directtaps.net/pr/svc/rightsmanager.asmx";
            Uri keyDeliveryServiceUri = new Uri(keyDeliveryServiceUriStr);

            string xmlFileName = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\..\..", @"MediaEncryptor_PlayReadyProtection.xml");      

            // Prepare the encryption task template
            System.Xml.Linq.XDocument doc = System.Xml.Linq.XDocument.Load(xmlFileName);

            var licenseAcquisitionUrlEl = doc
                    .Descendants(xmlns + "property")
                    .Where(p => p.Attribute("name").Value == "licenseAcquisitionUrl")
                    .FirstOrDefault();
            var contentKeyEl = doc
                    .Descendants(xmlns + "property")
                    .Where(p => p.Attribute("name").Value == "contentKey")
                    .FirstOrDefault();
            var keyIdEl = doc
                    .Descendants(xmlns + "property")
                    .Where(p => p.Attribute("name").Value == "keyId")
                    .FirstOrDefault();

            // Update the "value" property for each element.
            if (licenseAcquisitionUrlEl != null)
                licenseAcquisitionUrlEl.Attribute("value").SetValue(keyDeliveryServiceUri);

            if (contentKeyEl != null)
                contentKeyEl.Attribute("value").SetValue(contentKeyB64);

            if (keyIdEl != null)
                keyIdEl.Attribute("value").SetValue(keyId);

            doc.Save(xmlFileName);
        }

        public static byte[] GenerateCryptographicallyStrongRandomBytes(int length)
        {
            byte[] bytes = new byte[length];
            //This type implements the IDisposable interface. When you have finished using the type, you should dispose of it either directly or indirectly. To dispose of the type directly, call its Dispose method in a try/catch block. To dispose of it indirectly, use a language construct such as using (in C#) 
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                rng.GetBytes(bytes);
            }
            return bytes;
        }

        //generate a PlayReady content key: cryptographically strong random byte[16]
        public static string GeneratePlayReadyContentKey()
        {
            byte[] bytes = GenerateCryptographicallyStrongRandomBytes(16);
            return Convert.ToBase64String(bytes);
        }

        public static string GeneratePlayReadyKeySeed()
        {
            byte[] bytes = GenerateCryptographicallyStrongRandomBytes(30);   //30 for key seed: http://msdn.microsoft.com/en-us/library/hh973610.aspx
            return Convert.ToBase64String(bytes);
        }

        public static string GenerateSymmetricHashKey()
        {
            byte[] bytes = GenerateCryptographicallyStrongRandomBytes(32);  
            return Convert.ToBase64String(bytes);
        }

        //This API works the same as AESContentKey constructor in PlayReady Server SDK 
        public static string GetPlayReadyContentKeyFromKeyIdKeySeed(string keyIdString, string keySeedB64)
        {
            Guid keyId = new Guid(keyIdString);
            byte[] keySeed = Convert.FromBase64String(keySeedB64);

            byte[] contentKey = CommonEncryption.GeneratePlayReadyContentKey(keySeed, keyId);

            string contentKeyB64 = Convert.ToBase64String(contentKey);

            return contentKeyB64;
        }

        public static IContentKey ConfigureKeyDeliveryService(CloudMediaContext objCloudMediaContext, Guid keyId, string contentKeyB64, string assetId)
        {
            //check if the keyId exists
            var keys = objCloudMediaContext.ContentKeys.Where(k => k.Id == "nb:kid:UUID:" + keyId.ToString());
            if (keys.Count() > 0)
            {
                Console.WriteLine("Key Delivery for Key ID = {0} exists.", string.Format("nb:kid:UUID:{0}", keyId.ToString()));
                return null;
            }

            byte[] keyValue = Convert.FromBase64String(contentKeyB64);

            var contentKey = objCloudMediaContext.ContentKeys.Create(keyId, keyValue, string.Format("KID_{0}", keyId.ToString()), ContentKeyType.CommonEncryption);

            var restrictions = new List<ContentKeyAuthorizationPolicyRestriction>
                {
                    //ContentKeyRestrictionType.Open
                    //new ContentKeyAuthorizationPolicyRestriction { Requirements = null, 
                    //                                               Name = "Open", 
                    //                                               KeyRestrictionType = (int) ContentKeyRestrictionType.Open
                    //                                             }
                    
                    //ContentKeyRestrictionType.IPRestricted,    sample asset: http://willzhanmediaservice.origin.mediaservices.windows.net/394ade06-2d0b-4d5d-9fcc-dca67f9116ae/Anna.ism/Manifest
                    //new ContentKeyAuthorizationPolicyRestriction { Requirements = string.Format("<Allowed addressType=\"IPv4\"><AddressRange start=\"{0}\" end=\"{0}\" /></Allowed>", "67.186.67.74"), 
                    //                                               Name = "IPRestricted",                                    
                    //                                               KeyRestrictionType = (int) ContentKeyRestrictionType.IPRestricted 
                    //                                             }   

                    //ContentKeyRestrictionType.TokenRestricted, sample asset: http://willzhanmediaservice.origin.mediaservices.windows.net/0bd8e2fd-e508-4eac-b5b8-f10d95cbe9de/BigBuckBunny.ism/manifest
                    //new ContentKeyAuthorizationPolicyRestriction { Requirements = ContentKeyAuthorizationHelper.CreateRestrictionRequirements(),  //(ContentKeyAuthorizationHelper.AccessPolicyTemplateKeyClaim), 
                    //                                               Name = "TokenRestricted", 
                    //                                               KeyRestrictionType = (int) ContentKeyRestrictionType.TokenRestricted
                    //                                             }
                    
                    new ContentKeyAuthorizationPolicyRestriction { Requirements = ContentKeyAuthorizationHelper.CreateRestrictionRequirementsForJWT(),  //(ContentKeyAuthorizationHelper.AccessPolicyTemplateKeyClaim), 
                                                                   Name = "JWTContentKeyAuthorizationPolicyRestriction", 
                                                                   KeyRestrictionType = (int) ContentKeyRestrictionType.TokenRestricted
                                                                 }
                };

            //Name IContentKeyAuthorizationPolicy as IAsset.Id so that it is easy to be cleaned up
            IContentKeyAuthorizationPolicy contentKeyAuthorizationPolicy = objCloudMediaContext.ContentKeyAuthorizationPolicies.CreateAsync(assetId).Result;

            //Use License Template API
            string newLicenseTemplate = CreatePRLicenseResponseTemplate();

            //Name IContentKeyAuthorizationPolicyOption as IAsset.Id so that it is easy to be cleaned up
            IContentKeyAuthorizationPolicyOption policyOption = objCloudMediaContext.ContentKeyAuthorizationPolicyOptions.Create(assetId, ContentKeyDeliveryType.PlayReadyLicense, restrictions, newLicenseTemplate);

            contentKeyAuthorizationPolicy.Options.Add(policyOption);

            // Associate the content key authorization policy with the content key
            contentKey.AuthorizationPolicyId = contentKeyAuthorizationPolicy.Id;   //or contentKey.AuthorizationPolicy = policy;        
            contentKey = contentKey.UpdateAsync().Result;


            // Update the MediaEncryptor_PlayReadyProtection.xml file with the key and URL info.
            Uri keyDeliveryServiceUri = contentKey.GetKeyDeliveryUrl(ContentKeyDeliveryType.PlayReadyLicense);
            Console.WriteLine(string.Format("LAURL = {0}", keyDeliveryServiceUri.OriginalString));

            return contentKey;
        }

        public static IContentKey ConfigureKeyDeliveryService_Unprotected(CloudMediaContext objCloudMediaContext, Guid keyId, string contentKeyB64)
        {
            //check if the keyId exists
            var keys = objCloudMediaContext.ContentKeys.Where(k => k.Id == "nb:kid:UUID:" + keyId.ToString());
            if (keys.Count() > 0)
            {
                Console.WriteLine("Key Delivery for Key ID = {0} exists.", string.Format("nb:kid:UUID:{0}", keyId.ToString()));
                return null;
            }

            byte[] keyValue = Convert.FromBase64String(contentKeyB64);

            var contentKey = objCloudMediaContext.ContentKeys.Create(keyId, keyValue, string.Format("KID_{0}", keyId.ToString()), ContentKeyType.CommonEncryption);

            //create an authorization policy which is empty: no IContentKeyAuthorizationPolicyOption
            IContentKeyAuthorizationPolicy contentKeyAuthorizationPolicy = objCloudMediaContext.ContentKeyAuthorizationPolicies.CreateAsync("ContentKeyAuthorizationPolicy").Result;

            // Associate the content key authorization policy with the content key
            contentKey.AuthorizationPolicyId = contentKeyAuthorizationPolicy.Id;   //or contentKey.AuthorizationPolicy = policy;        
            contentKey = contentKey.UpdateAsync().Result;

            return contentKey;
        }


        public static void ListContentKeys(CloudMediaContext objCloudMediaContext)
        {
            string FORMAT1 = "\t{0,-26} = {1,-60}";
            string FORMAT2 = "\t\t{0,-26} = {1,-60}";
            string FORMAT3 = "\t\t\t{0,-26} = {1,-60}";
            string CLASS = "***[CLASS]***";
            //Console.WriteLine(FORMAT, "Key ID", "AuthNPolicyID", "Key Name", "ProtectionKeyId", "ProtectionKeyType", "Content Key", "ContentKeyType", "Created", "LastModified", "LAURL");
            IList<IAsset> objIList_IAsset;
            string url;
            ContentKeyDeliveryType contentKeyDeliveryType;

            //get content key-Assets dictionary
            Dictionary<string, IList<IAsset>> objDictionary = CollectKeyIdAssets(objCloudMediaContext);

            foreach (IContentKey objIContentKey in objCloudMediaContext.ContentKeys.OrderByDescending(k => k.LastModified))
            {
                switch (objIContentKey.ContentKeyType)
                {
                    case ContentKeyType.EnvelopeEncryption:
                    case ContentKeyType.CommonEncryption:
                        Console.WriteLine("{0} IContentKey:", CLASS);
                        Console.WriteLine(FORMAT1, "Id", objIContentKey.Id);                                                                                          //key ID with WAMS prefix: nb:kid:UUID:{KID-GUID}
                        Console.WriteLine(FORMAT1, "AuthorizationPolicyId", objIContentKey.AuthorizationPolicyId);                                                    //AuthNPolicyID
                        Console.WriteLine(FORMAT1, "Name", objIContentKey.Name);                                                                                      //key name
                        Console.WriteLine(FORMAT1, "ProtectionKeyId", objIContentKey.ProtectionKeyId);                                                                //X.509 cert thumbprint used to encrypt for storage
                        Console.WriteLine(FORMAT1, "ProtectionKeyType", objIContentKey.ProtectionKeyType.ToString());
                        Console.WriteLine(FORMAT1, "GetClearKeyValue()", Convert.ToBase64String(objIContentKey.GetClearKeyValue()));                                  //content key
                        Console.WriteLine(FORMAT1, "ContentKeyType", objIContentKey.ContentKeyType);                                                                  //CommonEncryption (for PlayReady protection)
                        Console.WriteLine(FORMAT1, "Created", objIContentKey.Created.ToString("MM-dd-yyyy HH:mm:ss"));
                        Console.WriteLine(FORMAT1, "LastModified", objIContentKey.LastModified.ToString("MM-dd-yyyy HH:mm:ss"));
                        contentKeyDeliveryType = objIContentKey.ContentKeyType == ContentKeyType.CommonEncryption? ContentKeyDeliveryType.PlayReadyLicense : ContentKeyDeliveryType.BaselineHttp;
                        Console.WriteLine(FORMAT1, "GetkeyDeliveryUrl()", objIContentKey.GetKeyDeliveryUrl(contentKeyDeliveryType).OriginalString);  //LAURL
                        //ContentKeyAuthorizationPolicy, ContentKeyAuthorizationPolicyOption, ContentKeyAuthorizationOptionRestriction
                        var policies = objCloudMediaContext.ContentKeyAuthorizationPolicies.Where(p => p.Id == objIContentKey.AuthorizationPolicyId);
                        
                        foreach (var policy in policies)
                        {
                            Console.WriteLine("{0} IContentKeyAuthorizationPolicy:", CLASS);
                            Console.WriteLine(FORMAT1, "Id", policy.Id);
                            Console.WriteLine(FORMAT1, "Name", policy.Name);
                            IList<IContentKeyAuthorizationPolicyOption> objIList_option = policy.Options;
                            foreach(var option in objIList_option)
                            {
                                Console.WriteLine(FORMAT1, "Options", string.Format("{0} IContentKeyAuthorizationPolicyOption:", CLASS));
                                Console.WriteLine(FORMAT2, "Name", option.Name);
                                Console.WriteLine(FORMAT2, "KeyDeliveryConfiguration", FormatXmlString(option.KeyDeliveryConfiguration));
                                Console.WriteLine(FORMAT2, "KeyDeliveryType", option.KeyDeliveryType);
                                List<ContentKeyAuthorizationPolicyRestriction> objList_restriction = option.Restrictions;
                                foreach (var restriction in objList_restriction)
                                {
                                    //KeyRestrictionType: Open, IPRestricted, TokenRestricted
                                    Console.WriteLine(FORMAT2, "Restrictions",  string.Format("{0} ContentKeyAuthorizationPolicyRestriction:", CLASS));
                                    Console.WriteLine(FORMAT3, "Name", restriction.Name);
                                    Console.WriteLine(FORMAT3, "KeyRestrictionType", restriction.KeyRestrictionType);
                                    Console.WriteLine(FORMAT3, "Requirements", FormatXmlString(restriction.Requirements));
                                }
                            }
                        }

                        //list IAssets with this IContentKey
                        if (objDictionary.ContainsKey(objIContentKey.Id))   //for failed job, a content key may not map to an existing IAsset
                        {
                            objIList_IAsset = objDictionary[objIContentKey.Id];
                            foreach (IAsset objIAsset in objIList_IAsset)
                            {
                                url = GetOrignUrl(objIAsset, objCloudMediaContext);
                                Console.WriteLine("{0} IAsset:", CLASS);
                                Console.WriteLine(FORMAT1, "Id", objIAsset.Id);
                                Console.WriteLine(FORMAT1, "Name", objIAsset.Name);
                                Console.WriteLine(FORMAT1, "LastModified",  objIAsset.LastModified.ToString("MM/dd/yyyy"));
                                Console.WriteLine(FORMAT1, "OriginUrl", url);

                                //IAssetDeliveryPolicy
                                Console.WriteLine("{0} IAssetDeliveryPolicy:", CLASS);
                                foreach(IAssetDeliveryPolicy objIAssetDeliveryPolicy in objIAsset.DeliveryPolicies)
                                {
                                    Console.WriteLine(FORMAT2, "AssetDeliveryPolicyType", objIAssetDeliveryPolicy.AssetDeliveryPolicyType.ToString());
                                    Console.WriteLine(FORMAT2, "AssetDeliveryProtocol",   objIAssetDeliveryPolicy.AssetDeliveryProtocol.ToString());
                                    Console.WriteLine(FORMAT2, "AssetDeliveryConfiguration", "(see below)");
                                    if (objIAssetDeliveryPolicy.AssetDeliveryConfiguration != null)
                                    {
                                        foreach (var key in objIAssetDeliveryPolicy.AssetDeliveryConfiguration.Keys)
                                        {
                                            Console.WriteLine(FORMAT3, key.ToString(), objIAssetDeliveryPolicy.AssetDeliveryConfiguration[key].ToString());
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("ContentKey: {0} does not have any associated IAsset", objIContentKey.Id);
                        }
                        Console.WriteLine(string.Empty);
                        break;
                    default:
                        Console.WriteLine("*******************" + objIContentKey.ContentKeyType.ToString());
                        if (objDictionary.Keys.Contains(objIContentKey.Id))  //if in the dictionary
                        {
                            objIList_IAsset = objDictionary[objIContentKey.Id];
                            foreach (IAsset objIAsset in objIList_IAsset)
                            {
                                Console.WriteLine("{0} IAsset:", CLASS);
                                Console.WriteLine(FORMAT1, "Id", objIAsset.Id);
                                Console.WriteLine(FORMAT1, "Name", objIAsset.Name);
                                Console.WriteLine(FORMAT1, "LastModified", objIAsset.LastModified.ToString("MM/dd/yyyy"));
                            }
                        }
                        else
                        {
                            Console.WriteLine("IContentKey.Name = {0}, IContentKey.LastModified = {1}", objIContentKey.Name, objIContentKey.LastModified.ToString("MM-dd-yyyy HH:mm:ss"));
                        }
                        break;
                }
                
            }
        }

        public static void ListAssetContentKeys(CloudMediaContext objCloudMediaContext)
        {
            string FORMAT = "{0,-60}{1,-60}{2,-60}{3,-60}{4,-60}{5,-50}{6,-30}{7,-30}{8,-30}";
            Console.WriteLine(FORMAT, "Asset ID", "Key ID", "Key Name", "ProtectionKeyId", "ProtectionKeyType", "Content Key", "ContentKeyType", "LastModified", "LAURL");

            IList<IContentKey> objIList_IContentKey;
            var assets = objCloudMediaContext.Assets;
            foreach(IAsset objIAsset in assets)
            {
                objIList_IContentKey = objIAsset.ContentKeys;
                if (objIList_IContentKey.Count > 0)
                {
                    foreach(IContentKey objIContentKey in objIList_IContentKey)
                    {
                        Console.WriteLine(FORMAT, objIAsset.Id,
                                                  objIContentKey.Id,                                                                         //key ID with WAMS prefix
                                                  objIContentKey.Name,                                                                       //key name
                                                  objIContentKey.ProtectionKeyId,
                                                  objIContentKey.ProtectionKeyType.ToString(),
                                                  Convert.ToBase64String(objIContentKey.GetClearKeyValue()),                                 //content key
                                                  objIContentKey.ContentKeyType,                                                             //CommonEncryption (for PlayReady protection)
                                                  objIContentKey.LastModified.ToString("MM-dd-yyyy"),
                                                  objIContentKey.GetKeyDeliveryUrl(ContentKeyDeliveryType.PlayReadyLicense).OriginalString
                                         );
                    }
                }
                else
                {
                    Console.WriteLine(string.Format("{0,-60}", objIAsset.Id));
                }
            }

        }

        //Collect all key ID's and their corresponding IAssets and put in a dictionary for lookup
        //HLS IAsset.ContentKeys does not contain the proper content key. Bug 685182 “Packager MP should set the AssetCreationOption to CommonEncryptionProtected and add the Common Encryption content key to the asset when HLS from a CommonEncryptionProtected Smooth asset” 
        public static Dictionary<string, IList<IAsset>> CollectKeyIdAssets(CloudMediaContext objCloudMediaContext)
        {
            Dictionary<string, IList<IAsset>> objDictionary = new Dictionary<string, IList<IAsset>>();
            IList<IAsset> objIList_IAsset;
            IList<IContentKey> objIList_IContentKey;

            var assets = objCloudMediaContext.Assets.ToList().OrderByDescending(a => a.LastModified); //sort by date
            foreach (IAsset objIAsset in assets)
            {
                objIList_IContentKey = objIAsset.ContentKeys;
                if (objIList_IContentKey.Count > 0)
                {
                    foreach (IContentKey objIContentKey in objIList_IContentKey)
                    {
                        if (!objDictionary.ContainsKey(objIContentKey.Id))
                        {
                            objIList_IAsset = new List<IAsset>();
                            objDictionary.Add(objIContentKey.Id, objIList_IAsset);
                        }
                        objIList_IAsset = objDictionary[objIContentKey.Id];
                        objIList_IAsset.Add(objIAsset);  
                    }
                }
            }

            return objDictionary;
        }

        public static string GetOrignUrl(IAsset objIAsset, CloudMediaContext objCloudMediaContext)
        {
            ILocator objILocator = null;
            string url = string.Empty;

            var assetLocators = objIAsset.Locators.ToList();
            foreach (var assetLocator in assetLocators)
            {
                //It must be origin
                if (assetLocator.Type == LocatorType.OnDemandOrigin)
                {
                    //It must not expire for the next 5 minutes:
                    if (assetLocator.ExpirationDateTime > DateTime.UtcNow.AddMinutes(5))
                    {
                        //Use it:
                        objILocator = assetLocator;
                        break;
                    }
                }
            }

            if (objILocator != null)
            {
                var theManifest = from f in objILocator.Asset.AssetFiles where f.Name.EndsWith(".ism") select f;
                // Cast the reference to a true IAssetFile type. 
                IAssetFile objIAssetFile = theManifest.First();
                url = string.Format("{0}{1}/manifest", objILocator.Path, objIAssetFile.Name);
            }

            return url;
        }

        static string CreatePRLicenseResponseTemplate()
        {
            PlayReadyLicenseResponseTemplate objPlayReadyLicenseResponseTemplate = new PlayReadyLicenseResponseTemplate();
            objPlayReadyLicenseResponseTemplate.ResponseCustomData = string.Format("WAMS-SecureKeyDelivery, Time = {0}", DateTime.UtcNow.ToString("MM-dd-yyyy HH:mm:ss"));
            PlayReadyLicenseTemplate objPlayReadyLicenseTemplate = new PlayReadyLicenseTemplate();
            objPlayReadyLicenseResponseTemplate.LicenseTemplates.Add(objPlayReadyLicenseTemplate);

            //objPlayReadyLicenseTemplate.BeginDate        = DateTime.Now.AddHours(-1).ToUniversalTime();
            //objPlayReadyLicenseTemplate.ExpirationDate   = DateTime.Now.AddHours(10.0).ToUniversalTime();
            objPlayReadyLicenseTemplate.LicenseType      = PlayReadyLicenseType.Nonpersistent;
            objPlayReadyLicenseTemplate.AllowTestDevices = true;  //MinmumSecurityLevel: 150 vs 2000

            //objPlayReadyLicenseTemplate.PlayRight.CompressedDigitalAudioOpl = 300;
            //objPlayReadyLicenseTemplate.PlayRight.CompressedDigitalVideoOpl = 400;
            //objPlayReadyLicenseTemplate.PlayRight.UncompressedDigitalAudioOpl = 250;
            //objPlayReadyLicenseTemplate.PlayRight.UncompressedDigitalVideoOpl = 270;
            //objPlayReadyLicenseTemplate.PlayRight.AnalogVideoOpl = 100;
            //objPlayReadyLicenseTemplate.PlayRight.AgcAndColorStripeRestriction = new AgcAndColorStripeRestriction(1);
            objPlayReadyLicenseTemplate.PlayRight.AllowPassingVideoContentToUnknownOutput = UnknownOutputPassingOption.Allowed;
            //objPlayReadyLicenseTemplate.PlayRight.ExplicitAnalogTelevisionOutputRestriction = new ExplicitAnalogTelevisionRestriction(0, true);
            //objPlayReadyLicenseTemplate.PlayRight.ImageConstraintForAnalogComponentVideoRestriction = true;
            //objPlayReadyLicenseTemplate.PlayRight.ImageConstraintForAnalogComputerMonitorRestriction = true;
            //objPlayReadyLicenseTemplate.PlayRight.ScmsRestriction = new ScmsRestriction(2);

            string serializedPRLicenseResponseTemplate = MediaServicesLicenseTemplateSerializer.Serialize(objPlayReadyLicenseResponseTemplate);

            //PlayReadyLicenseResponseTemplate responseTemplate2 = MediaServicesLicenseTemplateSerializer.Deserialize(serializedPRLicenseResponseTemplate);

            return serializedPRLicenseResponseTemplate;
        }

        private static string FormatXmlString(string xmlString)
        {
            if (string.IsNullOrEmpty(xmlString))
            {
                return xmlString;
            }
            else
            {
                System.Xml.Linq.XElement element = System.Xml.Linq.XElement.Parse(xmlString);
                return element.ToString();
            }
        }

    }  //class

    public static class ContentKeyAuthorizationHelper
    {
        public static void RemoveAssetAccessEntities(CloudMediaContext objCloudMediaContext, IAsset objIAsset)
        {
            //Removing all locators associated with asset
            var tasks = objCloudMediaContext.Locators.Where(c => c.AssetId == objIAsset.Id)
                    .ToList()
                    .Select(locator => locator.DeleteAsync())
                    .ToArray();
            Task.WaitAll(tasks);
            Console.WriteLine("Deleting locators");

            //Removing all delivery policies associated with asset
            for (int j = 0; j < objIAsset.DeliveryPolicies.Count; j++)
            {
                objIAsset.DeliveryPolicies.RemoveAt(0);
            }
            Console.WriteLine("Deleting IAsset.DeliveryPolicies");

            //removing all content keys associated with assets
            for (int j = 0; j < objIAsset.ContentKeys.Count; j++)
            {
                objIAsset.ContentKeys.RemoveAt(0);
            }
            Console.WriteLine("Deleting IAsset.ContentKeys");

            Task<IMediaDataServiceResponse>[] deleteTasks = objCloudMediaContext.ContentKeyAuthorizationPolicies.Where(c => c.Name == objIAsset.Id).ToList().Select(policy => policy.DeleteAsync()).ToArray();
            Task.WaitAll(deleteTasks);

            deleteTasks = objCloudMediaContext.ContentKeyAuthorizationPolicyOptions.Where(c => c.Name == objIAsset.Id).ToList().Select(policyOption => policyOption.DeleteAsync()).ToArray();
            Task.WaitAll(deleteTasks);
        }

        /*This code generates the following XML:
        <TokenRestrictionTemplate xmlns:i="http://www.w3.org/2001/XMLSchema-instance" xmlns="http://schemas.microsoft.com/Azure/MediaServices/KeyDelivery/TokenRestrictionTemplate/v1">
          <AlternateVerificationKeys>
            <TokenVerificationKey i:type="SymmetricVerificationKey">
              <KeyValue>hhv74VzJ+pOiuYw1z2wxh6ZkX4tRl/WVhBTvM6T/vUo=</KeyValue>
            </TokenVerificationKey>
          </AlternateVerificationKeys>
          <Audience>urn:test</Audience>
          <Issuer>https://willzhanacs.accesscontrol.windows.net/</Issuer>
          <PrimaryVerificationKey i:type="SymmetricVerificationKey">
            <KeyValue>7V1WtGGAylmZTMKA8RlVMrPNhukYBF2sW04UMpuD8bw=</KeyValue>
          </PrimaryVerificationKey>
          <RequiredClaims>
            <TokenClaim>
              <ClaimType>urn:microsoft:azure:mediaservices:contentkeyidentifier</ClaimType>
              <ClaimValue i:nil="true" />
            </TokenClaim>
          </RequiredClaims>
        </TokenRestrictionTemplate>
         * 
        <TokenRestrictionTemplate xmlns:i="http://www.w3.org/2001/XMLSchema-instance" xmlns="http://schemas.microsoft.com/Azure/MediaServices/KeyDelivery/TokenRestrictionTemplate/v1">
          <AlternateVerificationKeys>
            <TokenVerificationKey i:type="SymmetricVerificationKey">
              <KeyValue>hhv74VzJ+pOiuYw1z2wxh6ZkX4tRl/WVhBTvM6T/vUo=</KeyValue>
            </TokenVerificationKey>
          </AlternateVerificationKeys>
          <Audience>urn:test</Audience>
          <Issuer>https://willzhanacs.accesscontrol.windows.net/</Issuer>
          <PrimaryVerificationKey i:type="SymmetricVerificationKey">
            <KeyValue>7V1WtGGAylmZTMKA8RlVMrPNhukYBF2sW04UMpuD8bw=</KeyValue>
          </PrimaryVerificationKey>
          <RequiredClaims />
        </TokenRestrictionTemplate>
        */
        //Sample code: https://raw.githubusercontent.com/Azure/azure-media-services-samples/master/KDWithACS/ConsoleApplication6/Program.cs 
        public static string CreateRestrictionRequirements()
        {
            string primarySymmetricKey   = System.Configuration.ConfigurationManager.AppSettings["PrimarySymmetricKey"];
            string secondarySymmetricKey = System.Configuration.ConfigurationManager.AppSettings["SecondarySymmetricKey"];
            string scope                 = System.Configuration.ConfigurationManager.AppSettings["AcsScope"];
            string issuer                = System.Configuration.ConfigurationManager.AppSettings["AcsIssuer"];

            TokenRestrictionTemplate objTokenRestrictionTemplate = new TokenRestrictionTemplate();

            objTokenRestrictionTemplate.PrimaryVerificationKey = new SymmetricVerificationKey(Convert.FromBase64String(primarySymmetricKey));
            objTokenRestrictionTemplate.AlternateVerificationKeys.Add(new SymmetricVerificationKey(Convert.FromBase64String(secondarySymmetricKey)));
            objTokenRestrictionTemplate.Audience               = new Uri(scope);
            objTokenRestrictionTemplate.Issuer                 = new Uri(issuer);
            //objTokenRestrictionTemplate.RequiredClaims.Add(TokenClaim.ContentKeyIdentifierClaim);
            objTokenRestrictionTemplate.TokenType              = TokenType.SWT;   //new change: the default is JWT, a "gotcha"

            return TokenRestrictionTemplateSerializer.Serialize(objTokenRestrictionTemplate);
        }

        //for JWT token issued by AAD
        public static string CreateRestrictionRequirementsForJWT()
        {
            string audience = System.Configuration.ConfigurationManager.AppSettings["ida:audience"];
            string issuer   = System.Configuration.ConfigurationManager.AppSettings["ida:issuer"];

            List<X509Certificate2> objList_cert = GetX509Certificate2FromADMetadataEndpoint();

            TokenRestrictionTemplate objTokenRestrictionTemplate = new TokenRestrictionTemplate();

            objTokenRestrictionTemplate.PrimaryVerificationKey = new X509CertTokenVerificationKey(objList_cert[0]);
            objList_cert.GetRange(1, objList_cert.Count - 1).ForEach(c => objTokenRestrictionTemplate.AlternateVerificationKeys.Add(new X509CertTokenVerificationKey(c)));
            objTokenRestrictionTemplate.Audience               = new Uri(audience);
            objTokenRestrictionTemplate.Issuer                 = new Uri(issuer);
            objTokenRestrictionTemplate.TokenType              = TokenType.JWT;

            //add required claims
            string entitledGroupObjectId = System.Configuration.ConfigurationManager.AppSettings["ida:EntitledGroupObjectId"];
            objTokenRestrictionTemplate.RequiredClaims.Add(new TokenClaim("groups", entitledGroupObjectId));

            return TokenRestrictionTemplateSerializer.Serialize(objTokenRestrictionTemplate);
        }

        private static List<X509Certificate2> GetX509Certificate2FromADMetadataEndpoint()
        {
            List<X509Certificate2> objList_cert = new List<X509Certificate2>();
            XPathDocument xmlReader = new XPathDocument(System.Configuration.ConfigurationManager.AppSettings["ida:FederationMetadataLocation"]);
            XPathNavigator navigator = xmlReader.CreateNavigator();
            XmlNamespaceManager manager = new XmlNamespaceManager(navigator.NameTable);
            manager.AddNamespace("", "urn:oasis:names:tc:SAML:2.0:metadata");
            manager.AddNamespace("ns1", "urn:oasis:names:tc:SAML:2.0:metadata");
            manager.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");
            manager.PushScope();
            XPathNodeIterator nodes =
                navigator.Select(
                    "//ns1:EntityDescriptor/ns1:RoleDescriptor/ns1:KeyDescriptor[@use='signing']/ds:KeyInfo/ds:X509Data/ds:X509Certificate",
                    manager);
            
            //read in all certs
            while (nodes.MoveNext())
            {
                XPathNavigator nodesNavigator = nodes.Current;
                //Cert body is base64 encoded in metadata doc
                objList_cert.Add(new X509Certificate2(Convert.FromBase64String(nodesNavigator.InnerXml)));
            }

            return objList_cert;
        }


        //E.g.: http%3a%2f%2fschemas.xmlsoap.org%2fws%2f2005%2f05%2fidentity%2fclaims%2fnameidentifier=willzhan&http%3a%2f%2fschemas.microsoft.com%2faccesscontrolservice%2f2010%2f07%2fclaims%2fidentityprovider=https%3a%2f%2fwillzhanacs.accesscontrol.windows.net%2f&Audience=urn%3atest&ExpiresOn=1415676458&Issuer=https%3a%2f%2fwillzhanacs.accesscontrol.windows.net%2f&HMACSHA256=e4sFoE9UdVoWgLb%2bbTXIG%2byoxOOEdyKCxQlI%2f8h%2bNx4%3d
        public static string GetToken(/*string clientId, string clientSecret, Uri scope, */Uri issuer)
        {
            string tokenToReturn = null;

            using (WebClient client = new WebClient())
            {
                //
                //  Create the authentication request to get a token
                //
                client.BaseAddress = issuer.AbsoluteUri;

                var oauthRequestValues = new NameValueCollection
                {
                    {"grant_type", "client_credentials"},
                    {"client_id", "willzhan"},
                    {"client_secret", "willzhanacs$1"},
                    {"scope", "urn:test"},
                };

                byte[] responseBytes = null;

                try
                {
                    responseBytes = client.UploadValues("/v2/OAuth2-13", "POST", oauthRequestValues);
                }
                catch (WebException we)
                {
                    //
                    //  We hit an exception trying to acquire the token.  Write out the response and then throw
                    //
                    Stream stream = we.Response.GetResponseStream();
                    StreamReader reader = new StreamReader(stream);
                    Console.WriteLine("Error response when trying to acquire the token: {0}", reader.ReadToEnd());

                    throw;
                }

                //
                //  Process the response from ACS to get the token
                //
                using (var responseStream = new MemoryStream(responseBytes))
                {
                    OAuth2TokenResponse tokenResponse = (OAuth2TokenResponse)new DataContractJsonSerializer(typeof(OAuth2TokenResponse)).ReadObject(responseStream);
                    tokenToReturn = tokenResponse.AccessToken;
                }
            }

            return tokenToReturn;
        }

        public static void Download(string token)
        {
            using (WebClient client = new WebClient()) 
             { 
                client.Headers["Authorization"] = "Bearer=" + token;
                byte[] downloadedKeyValue = client.DownloadData(new Uri("https://willzhanmediaservice.keydelivery.mediaservices.windows.net/PlayReady/"));


                Console.WriteLine(System.Text.Encoding.Default.GetString(downloadedKeyValue)); 
             } 

        }
    } //class


    [DataContract]
    public class OAuth2TokenResponse
    {
        /// <summary> 
        /// Gets or sets current access token value.
        /// </summary> 
        [DataMember(Name = "access_token")]
        public string AccessToken { get; set; }

        /// <summary> 
        /// Gets or sets current refresh token value. 
        /// </summary> 
        [DataMember(Name = "expires_in")]
        public int ExpirationInSeconds { get; set; }
    }

}  //namespace
