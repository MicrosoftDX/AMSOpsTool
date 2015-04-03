using System;
using System.Linq;
using System.Configuration;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;   //StorageCredentials
using Microsoft.WindowsAzure.Storage.Blob;   //Blob convenience implementation, applications utilizing Windows Azure Blobs 
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.Serialization.Json;
using System.Net;

namespace PrototypeConsole
{
    class Program
    {
        static CloudMediaContext objCloudMediaContext;
        private static System.Object consoleWriteLock = new Object();  //used in bulk ingest display

        static void Main(string[] args)
        {
            objCloudMediaContext = GetContext();
            Console.WriteLine("CloudMediaContext created successfully.");

            //variables
            string[] paths;
            string path;
            string outputFolder;
            string assetId;
            string configFilePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\..\..";  //@"C:\Workspace\CSharp\WAMSSoln\PrototypeConsole";
            string manifestName;
            IAsset objIAsset, objIAssetOut;
            IJob objIJob;
            ILocator objILocator;
            
            //*****SPECIFY WHICH CASE TO RUN *******/
            int id = 43;

            switch (id)
            {
                case 0:   //ingest a single file
                    path = @"C:\Workspace\Destination\Input\SingileFile\Funny Videos From Russia With Love.mp4";
                    objIAsset = CreateAssetAndUploadSingleFile(AssetCreationOptions.None, path);
                    Console.WriteLine(string.Format("Asset {0} created and uploaded.", objIAsset.Name));
                    break;
                case 1:   //ingest multiple files in a folder
                    path = @"C:\Workspace\MCS Manufacturing & Engineering\Training\PlayReady\SmoothStreaming\VideoSource\TestAds\JoyRide";
                    //path = @"C:\Workspace\MCS Manufacturing & Engineering\Training\PlayReady\SmoothStreaming\VideoSource\TestAds\DanceCentral";
                    objIAsset = CreateAssetAndUploadMultipleFiles("JoyRide", AssetCreationOptions.None, path);
                    Console.WriteLine(string.Format("Asset {0} created and uploaded.", objIAsset.Name));
                    break;
                case 2:   //Media Processor
                    string[] mediaProcessNames = new string[] { "Windows Azure Media Encoder", "Windows Azure Media Packager", "Windows Azure Media Encryptor", "Storage Decryption" };
                    IMediaProcessor objIMediaProcessor;
                    foreach (string mediaProcessName in mediaProcessNames)
                    {
                        objIMediaProcessor = GetLatestMediaProcessorByName(mediaProcessName);
                        Console.WriteLine(string.Format("Media Processor: {0}, {1}, {2}, {3}", objIMediaProcessor.Id, objIMediaProcessor.Name, objIMediaProcessor.Vendor, objIMediaProcessor.Version));
                    }
                    break;
                case 3:   //Simple job
                    path = @"C:\Workspace\Destination\Input\SingileFile\TVG_EVP_Greenberg_MCS.wmv";
                    objIJob = CreateSimpleJob(path);
                    break;
                case 4:   //MP4 to Smooth Streaming to PlayReady protection
                    //path = @"C:\Workspace\Destination\Input\SingileFile\Funny Videos From Russia With Love.mp4";
                    path = @"C:\Workspace\Destination\Input\MultipleFile\MP4";
                    objIJob = CreatePlayReadyProtectionJob(path, configFilePath);
                    break;
                case 40:
                    //static PlayReady protection flow
                    CryptoUtils.DoVodDrmFlow(objCloudMediaContext);
                    break;
                case 41:  //PlayReady dynamic protection for DASH and smooth
                    objIAsset = GetAsset("nb:cid:UUID:b2e86717-4544-45de-b3d8-a44a9e0d3062"); //Microsoft_HoloLens_TransformYourWorld_816p23-mp4-H264_Adaptive_Bitrate_MP4_Set_1080p-Output       
                    ContentKeyAuthorizationHelper.RemoveAssetAccessEntities(objCloudMediaContext, objIAsset);
                    CryptoUtils.SetupDynamicPlayReadyProtection(objCloudMediaContext, objIAsset);
                    GetStreamingOriginLocator(objIAsset.Id, MediaContentType.SmoothStreaming, true);   //publish
                    //ListILocators("nb:cid:UUID:e80356c9-f9ef-40c3-9750-d27c7760ed74");
                    //Console.WriteLine(ContentKeyAuthorizationHelper.CreateRestrictionRequirements());
                    break;
                case 42:  //List all content keys and its detailed configurations
                    CryptoUtils.ListContentKeys(objCloudMediaContext);
                    //CryptoUtils.ListAssetContentKeys(objCloudMediaContext);
                    break;
                case 43:  //AES Dynamic Encryption for smooth, HLS, DASH
                    objIAsset = GetAsset("nb:cid:UUID:cf7d336d-bc26-4ae1-acdf-66870ce3e182");  //Microsoft_HoloLens_TransformYourWorld_816p23-mp4-H264_Adaptive_Bitrate_MP4_Set_720p-Output      
                    ContentKeyAuthorizationHelper.RemoveAssetAccessEntities(objCloudMediaContext, objIAsset);
                    AesEncryption.DynamicAesEncryptionFlow(objCloudMediaContext, objIAsset);
                    GetStreamingOriginLocator(objIAsset.Id, MediaContentType.SmoothStreaming, true);   //publish
                    break;
                case 44:  //Content key WITHOUT protection/encryption
                    objIAsset = GetAsset("nb:cid:UUID:fc0367ea-7a07-499b-aef1-1ab7d61ff1b4");
                    CryptoUtils.SetupContentKeyWithoutProtection(objCloudMediaContext, objIAsset);
                    break;
                case 45:  //Live dynamic PlayReady protection
                    CryptoUtils.SetupLiveDynamicPlayReadyProtection(objCloudMediaContext, "ch03", "prgliveplayreadydynamic", "willzhanmanifest");
                    break;
                case 46:  //Set up dynamic PlayReady protection using an existing IContentKey (multiple assets share the same IContentKey)
                    assetId = "nb:cid:UUID:06f3d4b3-2490-4ee4-8eb1-99a21befc3a8";  //RexonaCommercial-mp4-PCMac-Output2
                    objIAsset = GetAsset(assetId);
                    CryptoUtils.SetupDynamicPlayReadyProtectionWithExistingContentKey(objCloudMediaContext, objIAsset);
                    break;
                case 5:   //Asset and Job Management
                    //DeleteAllAssets(new string[] {"Pirelli", "NGOTW14", "Toyota", "Country"});
                    ListAssets();
                    //string assetId = "nb:cid:UUID:2ec176b8-3499-4af5-b2a7-df5649b03b09";
                    //objIAsset = GetAsset(assetId);
                    //DeleteJobs(100);
                    //ListAllJobs();
                    //IJob objIJob = GetJob("nb:jid:UUID:650c32f3-115c-e345-8aea-f13c4ea76f71");
                    //DeleteBulkManifest("IngestManifest4Workflow");
                    //ListILocators();
                    //ListAllMediaProcessors();
                    break;
                case 51:
                    //ListAssets();
                    GetJobInfo("nb:jid:UUID:8712445d-1500-80c2-cc43-f1e4c14ba164", objCloudMediaContext);
                    break;
                case 6:   //Get origin locator for either adpative streaming or progressive download
                    //Smooth streaming output asset: nb:cid:UUID:8bd40b3c-d456-4500-8647-dbbe990f7af1
                    objILocator = GetStreamingOriginLocator("nb:cid:UUID:d29545ff-0300-80bd-5b22-f1e4b3f81008", MediaContentType.ProgressiveDownload, true);
                    break;
                case 7:   //Rest API Test
                    TestRestApi();
                    break;
                case 8:   //download an asset by asset ID
                    objIAsset = DownloadAsset("nb:cid:UUID:ed3db2a4-e620-4244-a451-f655bb5c3879", @"C:\Workspace\Destination\Output");
                    break;
                case 9:   //complete workflow
                    path = @"C:\Workspace\Destination\Input\SingileFile\LyncSkypeSizzleVideo750k.mp4";
                    //paths = new string[1];
                    //paths[0] = @"C:\Workspace\Destination\Input\SingileFile\Subclip\";

                    ////Option 1: upload an asset using Bulk Ingest
                    //objIAsset = null;
                    //manifestName = "IngestManifest4Workflow";
                    //for (int i = 0; i < paths.Length; i++ )
                    //{
                    //    BulkIngestSingleAsset(manifestName, new string[] { paths[i] }, out objIAsset);
                    //    Console.WriteLine("UPLOADED ASSET - IAsset.Id = {0}", objIAsset.Id);
                    //}
                    
                    //Option 2: upload an asset using CreateAssetAndUploadSingleFile
                    objIAsset  = CreateAssetAndUploadSingleFile(AssetCreationOptions.None, path);

                    //Option 3: upload an asset using CreateAssetAndUploadMultipleFiles("JoyRide", AssetCreationOptions.None, path);
                    //objIAsset = CreateAssetAndUploadMultipleFiles("Build2014KeynoteSublcipping", AssetCreationOptions.None, paths[0]);

                    //Option 4: use an existing asset (uploaded via Aspera and copied into WAMS IAsset)
                    //assetId = "nb:cid:UUID:3011b596-4f98-401c-b980-310ae9712e8c";     //stitched HoloLens videos
                    //objIAsset  = GetAsset(assetId);

                    Console.WriteLine("IAsset.Id={0}", objIAsset.Id);

                    outputFolder = @"C:\Workspace\Destination\Output";
                    objIJob      = DoWorkflow(objIAsset, outputFolder, out objIAssetOut);
                    //objIJob      = HLSWorkflow(objIAsset, outputFolder, out objIAssetOut);
                    //objIJob      = IndexerWorkflow(objIAsset, outputFolder, out objIAssetOut);
                    //delete IIngestManifest, IIngestManifestAssets and IAssets
                    //DeleteBulkManifest(manifestName);
                    break;
                case 90:  //download asset
                    DownloadAsset("nb:cid:UUID:70b4f828-6a17-4eab-8b3f-f44c8d941eca", @"C:\Workspace\Destination\Output\MicrosoftHoloLens\");
                    break;
                case 91:  //Kayak workflow
                    //assetId = "nb:cid:UUID:627b97d2-6785-4b28-b338-8cc30d833903";
                    string[] assetIds = new string[] { 
                                                       "nb:cid:UUID:08933065-369d-4640-9ae0-5cce5470194a"
                                                     //, "nb:cid:UUID:3f286e38-fdcc-4e49-8b28-26b84fa3320b"
                                                     //, "nb:cid:UUID:72687f3e-8db3-49e6-ba7a-11a71548fb1c"
                                                     //, "nb:cid:UUID:34584282-e912-46aa-8865-6ab934ebdefa"
                                                     //, "nb:cid:UUID:ed825006-29fb-46cb-99ab-813303809689"
                                                     //, "nb:cid:UUID:8c2c0f99-270d-40e6-91cb-d346161e5a17"
                                                     //, "nb:cid:UUID:7ba5c0d5-430f-4f55-84b3-4b5a0ae1726c"
                                                     //, "nb:cid:UUID:20914977-a509-40a8-8b25-cde1a22bd4f4"
                                                     //, "nb:cid:UUID:ac7abdd9-c344-4d05-b86f-dbb4671bb087"
                                                     //, "nb:cid:UUID:3d1fe468-8dce-4000-9ae5-4d43e535e4dd"
                                                     //, "nb:cid:UUID:b5509e5a-8f9d-42f7-8570-f0273aee25c4"
                                                     //, "nb:cid:UUID:627b97d2-6785-4b28-b338-8cc30d833903"
                                                     //, "nb:cid:UUID:d237888e-eeae-4cfd-b19c-62ac555b2c77"
                                                     };
                    outputFolder = @"C:\Workspace\Destination\Output";
                    for (int i = 0; i < assetIds.Length; i++)
                    {
                        objIAsset = GetAsset(assetIds[i]);
                        objIJob = DoKayakWorkflow(objIAsset, outputFolder, out objIAssetOut);
                    }
                    break;
                case 92:  //Stitch video clips
                    //upload assets
                    //paths = new string[] { 
                    //                       @"C:\Workspace\Destination\Input\SingileFile\Aspen_Onsight_Online_816p24_FIXEDaudio.mp4",
                    //                       @"C:\Workspace\Destination\Input\SingileFile\Microsoft_HoloLens_Possibilities_816p24.mp4",
                    //                       @"C:\Workspace\Destination\Input\SingileFile\Microsoft_HoloLens_TransformYourWorld_816p23.mp4"
                    //                     };
                    //string[] assetNames = new string[] { "Aspen_Onsight_Online",             //
                    //                                     "HoloLens_Possibilities",           //
                    //                                     "HoloLens_TransformYourWorld",      //
                    //                                   };
                    
                    //for (int i = 0; i < paths.Length; i++)
                    //{
                    //    //objIAsset = CreateAssetAndUploadMultipleFiles(assetNames[i], AssetCreationOptions.None, paths[i]);
                    //    objIAsset = CreateAssetAndUploadSingleFile(AssetCreationOptions.None, paths[i]);
                    //}
                    
                    outputFolder = @"C:\Workspace\Destination\Output";
                    IAsset[] objIAssets = new IAsset[3];
                    objIAssets[0] = GetAsset("nb:cid:UUID:134b9153-1946-4e96-a533-975824a1e467");
                    objIAssets[1] = GetAsset("nb:cid:UUID:eea2a89f-2785-4a23-a89a-1802b1f7d386");
                    objIAssets[2] = GetAsset("nb:cid:UUID:e4ec3a0d-b8fa-4e72-8008-add2aad2486f");
                    objIJob = StitchVideoClips(objIAssets, outputFolder, out objIAssetOut);
                    break;
                case 10:  //Manifest bulk ingest
                    manifestName = "TestManifest";  //Reminder: change media service name/key in app.config
                    //DeleteBulkManifest(manifestName);
                    //CreateBulkIngestManifest(manifestName);
                    //ListIngestManifests();
                    //DeleteBulkManifest(manifestName);
                    string[] filePaths = new string[] { @"C:\Workspace\Destination\Input\MultipleFile\Silverlight\sl_230_10.ismv", @"C:\Workspace\Destination\Input\MultipleFile\Silverlight\sl_305_10.ismv" };
                    IIngestManifest objIIngestManifest = BulkIngestSingleAsset(manifestName, filePaths, out objIAsset);
                    //if you want to upload with HTTP instead of Aspera P2P
                    //foreach (string filePath in filePaths)
                    //{
                    //    UploadBlobFile(objIIngestManifest.BlobStorageUriForUpload, filePath);
                    //}
                    break;
                case 11:  //enumerate all files from a folder
                    foreach (string path1 in GetPathsFromFolder(@"C:\Workspace\Destination\Input\MultipleFile\Silverlight"))
                    {
                        Console.WriteLine(path1);
                    }
                    break;
                case 111:  //create ILocator of OnDemand type
                    GetStreamingOriginLocator("nb:cid:UUID:b2c9b679-87c5-4071-aaab-a94881c51bff", MediaContentType.HLS, true);
                    break;
                case 12:  //copy files from container in media service storage into an IAsset
                    //path = @"C:\Workspace\Destination\Input\MultipleFile\Silverlight";
                    string srcContainerName0 = "asset-b2e86717-4544-45de-b3d8-a44a9e0d3062";  //Microsoft_HoloLens_TransformYourWorld_816p23-mp4-H264_Adaptive_Bitrate_MP4_Set_1080p-Output
                    string destAssetName     = "HoloLens_TransformYourWorld_1080p_copy5";
                    CloudBlobClient objCloudBlobClient;
                    CloudBlobContainer objCloudBlobContainer = GetInternalCloudBlobContainer(srcContainerName0, out objCloudBlobClient);   //lower case required
                    //UploadFilesToCloudBlobContainer(objCloudBlobContainer, path);
                    objIAsset = CopyBlobsToMediaAsset(objCloudBlobContainer, objCloudBlobClient, destAssetName);
                    SetPrimaryFile(objIAsset.Id);
                    break;
                case 13:  //copy files from non-WAMS (external) storage to WAMS asset
                    //path = @"C:\Workspace\Destination\Input\MultipleFile\Silverlight";
                    CloudBlobClient objCloudBlobClientExternal, objCloudBlobClientInternal;
                    CloudBlobContainer objCloudBlobContainerInternal = GetInternalCloudBlobContainer("wzcontainer", out objCloudBlobClientInternal);
                    CloudBlobContainer objCloudBlobContainerExternal = GetExternalCloudBlobContainer("wzcontainerwest", out objCloudBlobClientExternal);   //lower case required
                    //UploadFilesToCloudBlobContainer(objCloudBlobContainerExternal, path);
                    objIAsset = CopyNonWAMSBlobsToMediaAsset(objCloudBlobContainerExternal, objCloudBlobClientInternal, "CopiedFromExternalStorageBlobs Asset" + Guid.NewGuid().ToString());
                    SetPrimaryFile(objIAsset.Id);
                    break;
                case 14:  //copy between storages and convert to WAMS IAsset
                    //3 configs: WAMS service, WAMS storage, non-WAMS storage
                    //container names are in lower case
                    string srcAccountName  = System.Configuration.ConfigurationManager.AppSettings["EastUSStorageAccountName"];
                    string srcAccountKey   = System.Configuration.ConfigurationManager.AppSettings["EastUSStorageAccountKey"];
                    string destAccountName = System.Configuration.ConfigurationManager.AppSettings["willzhanstoreAccountName"];
                    string destAccountKey  = System.Configuration.ConfigurationManager.AppSettings["willzhanstoreAccountKey"];

                    CloudBlobClient objCloudBlobClient1;
                    string[] srcContainerNames = new string[] { "nbcuvod" };
                    string[] destContainerNames = srcContainerNames;
                    DateTime start = DateTime.Now;
                    for (int i = 0; i < srcContainerNames.Length; i++)
                    {
                        Console.WriteLine("Copying from {0} to {1} container", srcContainerNames[i], destContainerNames[i]);
                        CopyBetweenStoages(srcContainerNames[i], srcAccountName, srcAccountKey, destContainerNames[i], destAccountName, destAccountKey);
                        //copy blob in WAMS storage to WAMS IAsset
                        objCloudBlobContainer = GetInternalCloudBlobContainer(destContainerNames[i], out objCloudBlobClient1);
                        //using destContainerNames[i] as IAsset name
                        Console.WriteLine("Copying container {0} to IAsset with the same asset name.", destContainerNames[i]);
                        objIAsset = CopyBlobsToMediaAsset(objCloudBlobContainer, objCloudBlobClient1, destContainerNames[i]);
                        Console.WriteLine("Container: {0}, IAsset.Id = {1}", destContainerNames[i], objIAsset.Id);
                    }
                    Console.WriteLine("Time taken: {0} (ms)", ((TimeSpan)(DateTime.Now - start)).TotalMilliseconds.ToString());
                    break;
                case 15:    //test updating .ism file for HLS 6-sec GOP/1:1 packing
                    string url = "http://vod.nbcsgliveprodeastms.origin.mediaservices.windows.net/2e6a417a-6195-41aa-993b-b93ff5c1859a/sl.ism/Manifest";
                    IsmSettings.UpdateIsm(url);
                    break;
                case 16:    //Scale Encoding RU
                    //NOTE: [1] must use a cert for client authentication. A cert for server auth will result in 403 (Forbidden). 
                    //      [2] cert must be imported into Azure for the right subscription
                    //http://msdn.microsoft.com/en-us/library/windowsazure/ee460782.aspx
                    X509Certificate2 objX509Certificate2 = GetStoreCertificate("86F095A63DFC5C2701A327DEA79F9C4CDBA83145");  //MSIT client auth, willzhan11.northamerica.corp.microsoft.com
                    Console.WriteLine(objX509Certificate2.Subject);
                    UpdateEncodingReservedUnits(objX509Certificate2, 0);
                    break;
                case 17:    //AzureStorageUtils test
                    //AzureStorageUtils.CreateContainerUploadFile("sastestcontainer", "sastestblob.txt");
                    //string sasUri = AzureStorageUtils.GetBlobSasUri("sastestcontainer", "sastestblob.txt");
                    //string sasUri = AzureStorageUtils.GetContainerSasUri("sastestcontainer");
                    //Console.WriteLine("SAS URI = {0}", sasUri);
                    AzureStorageUtils.UseContainerSAS("https://willzhanstorage.blob.core.windows.net/sastestcontainer?sv=2013-08-15&sr=c&sig=alr4pfsOZLmp%2FtXfuZfXKxhQyZwztCZPzlrdo1VnmeU%3D&se=2014-08-28T19%3A27%3A55Z&sp=rwdl");
                    break;
                default:
                    break;
            }

            Console.WriteLine("Hit any key to exit.");
            Console.ReadKey();
        }

        public static void Test()
        {
            CloudMediaContext context = new CloudMediaContext(new Uri("https://nimbuspartners.cloudapp.net/API"),
                                                              "test",
                                                              "IAsMI5nOTXJVgAQigGX8MBo41bqICBghCdGuUl8ysAM=",
                                                              "urn:NimbusPartners",
                                                              "https://mediaservices.accesscontrol.windows.net");
            // upload asset
            Console.WriteLine("Number of assets = {0}", objCloudMediaContext.Assets.Count());
            IAsset inputAsset1 = context.Assets.Create("mediaAsset", AssetCreationOptions.StorageEncrypted);
            //IAssetFile inputAsset1File1 = inputAsset1.AssetFiles.Create("Artbeats30secs.avi");
            //inputAsset1File1.Upload("Artbeats30secs.avi");
            //IAsset inputAsset2 = context.Assets.Create("graphAsset", AssetCreationOptions.StorageEncrypted);
            //IAssetFile inputAsset2File1 = inputAsset2.AssetFiles.Create("MP4_Multilayer_wAudio_FromSource_ISO_SMIL.graph");
            //inputAsset2File1.Upload("MP4_Multilayer_wAudio_FromSource_ISO_SMIL.graph");
            //IJob job = context.Jobs.Create("job");
            //ITask task = job.Tasks.AddNew("ask", context.MediaProcessors.Where(mp => mp.Name == "Digital Rapids - Kayak Cloud Engine").FirstOrDefault(), "<some configuration e.g. 216.191.138.100,15000,,.mp4>", TaskOptions.None);
            //task.InputAssets.Add(inputAsset1);
            //task.InputAssets.Add(inputAsset2);
            //IAsset output = task.OutputAssets.AddNew("output");
            //job.Submit();
            //// wait for job to complete
            Console.ReadLine();

        }

        #region General

        static CloudMediaContext GetContext()
        {
            // Gets the service context. 
            string serviceUrl  = System.Configuration.ConfigurationManager.AppSettings["ServiceUrl"];
            string scope       = System.Configuration.ConfigurationManager.AppSettings["Scope"];
            string acsAddress  = System.Configuration.ConfigurationManager.AppSettings["ACSAddress"];
            string accountName = System.Configuration.ConfigurationManager.AppSettings["MediaServiceAccountName"];
            string accountKey  = System.Configuration.ConfigurationManager.AppSettings["MediaServiceAccountKey"];

            //MediaServicesCredentials objMediaServicesCredentials = new MediaServicesCredentials(accountName, accountKey, scope, acsAddress);
            //objMediaServicesCredentials.RefreshToken();
            //CloudMediaContext objCloudMediaContext = new CloudMediaContext(new Uri(serviceUrl), objMediaServicesCredentials);
            CloudMediaContext objCloudMediaContext = new CloudMediaContext(new Uri(serviceUrl), accountName, accountKey, scope, acsAddress);
            return objCloudMediaContext;
        }

        #endregion

        #region Utilities

        public static void WriteToFile(string text, string path, Encoding objEncoding)
        {
            StreamWriter objStreamWriter = new StreamWriter(path, true, objEncoding);
            objStreamWriter.WriteLine(text);
            objStreamWriter.Close();
        }

        public static string[] GetPathsFromFolder(string folder)
        {
            if (Directory.Exists(folder))
            {
                IEnumerable<string> objIEnumeralbe_string = Directory.EnumerateFiles(folder);
                return objIEnumeralbe_string.ToArray<string>();
            }
            else
            {
                return null;
            }
        }

        //IAsset from the current job tasks cannot be used in downloading or publish. We have to use this approach to get hold of an asset.
        public static IAsset GetAssetFromJobAndAssetName(string jobId, string assetName)
        {
            IJob objIJob = GetJob(jobId);
            var assetInstance = from a in objIJob.OutputMediaAssets where a.Name == assetName select a;
            // Reference the asset as an IAsset.
            IAsset objIAsset = assetInstance.FirstOrDefault();

            return objIAsset;
        }

        #endregion

        #region Ingest

        static private IAsset CreateEmptyAsset(string assetName, AssetCreationOptions assetCreationOptions)
        {
            var asset = objCloudMediaContext.Assets.Create(assetName, assetCreationOptions);

            Console.WriteLine("Asset name = {0} ", asset.Name);
            Console.WriteLine("Asset.Id = {0}", asset.Id);
            Console.WriteLine("Time created: {0}", asset.Created.Date.ToString());

            return asset;
        }

        static public IAsset CreateAssetAndUploadSingleFile(AssetCreationOptions assetCreationOptions, string singleFilePath)
        {
            var assetName = "SingleFileUpload_" + DateTime.Now.ToString();
            var asset = CreateEmptyAsset(assetName, assetCreationOptions);

            var fileName = Path.GetFileName(singleFilePath);
            var assetFile = asset.AssetFiles.Create(fileName);
            Console.WriteLine("Created assetFile {0}", assetFile.Name);

            var accessPolicy = objCloudMediaContext.AccessPolicies.Create(assetName, TimeSpan.FromDays(3), AccessPermissions.Write | AccessPermissions.List);
            var locator = objCloudMediaContext.Locators.CreateLocator(LocatorType.Sas, asset, accessPolicy);
            Console.WriteLine("Upload {0}", assetFile.Name);

            assetFile.Upload(singleFilePath);
            Console.WriteLine("Done uploading of {0} using Upload()", assetFile.Name);
            Console.WriteLine("IAsset.Id = {0}", asset.Id);

            locator.Delete();
            accessPolicy.Delete();

            return asset;
        }

        static public IAsset CreateAssetAndUploadMultipleFiles(string assetName, AssetCreationOptions assetCreationOptions, string folderPath)
        {
            //var assetName = "UploadMultipleFiles_" + DateTime.Now.ToString();

            var asset = CreateEmptyAsset(assetName, assetCreationOptions);

            var accessPolicy = objCloudMediaContext.AccessPolicies.Create(assetName, TimeSpan.FromDays(30), AccessPermissions.Write | AccessPermissions.List);
            var locator = objCloudMediaContext.Locators.CreateLocator(LocatorType.Sas, asset, accessPolicy);

            var blobTransferClient = new BlobTransferClient();
            blobTransferClient.NumberOfConcurrentTransfers = 20;
            blobTransferClient.ParallelTransferThreadCount = 20;

            blobTransferClient.TransferProgressChanged += blobTransferClient_TransferProgressChanged;

            var filePaths = Directory.EnumerateFiles(folderPath);

            Console.WriteLine("There are {0} files in {1}", filePaths.Count(), folderPath);

            if (!filePaths.Any())
            {
                throw new FileNotFoundException(String.Format("No files in directory, check folderPath: {0}", folderPath));
            }

            List<Task> objList_Task = new List<Task>();
            foreach (var filePath in filePaths)
            {
                var assetFile = asset.AssetFiles.Create(Path.GetFileName(filePath));
                Console.WriteLine("Created assetFile {0}", assetFile.Name);

                // It is recommended to validate AccestFiles before upload. 
                Console.WriteLine("Start uploading of {0}", assetFile.Name);
                objList_Task.Add(assetFile.UploadAsync(filePath, blobTransferClient, locator, CancellationToken.None));
            }

            Task.WaitAll(objList_Task.ToArray());
            Console.WriteLine("Done uploading the files");

            blobTransferClient.TransferProgressChanged -= blobTransferClient_TransferProgressChanged;

            locator.Delete();
            accessPolicy.Delete();

            return asset;
        }

        static void blobTransferClient_TransferProgressChanged(object sender, BlobTransferProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage > 4) // Avoid startup jitter, as the upload tasks are added.
            {
                Console.WriteLine("{0}% upload completed for {1}.", e.ProgressPercentage, e.LocalFile);
            }
        }

        #endregion
        
        #region IIngestManifest and Aspera P2P transfer workflow

        static IIngestManifest CreateBulkIngestManifest(string name)
        {
            Console.WriteLine("\n===============================================");
            Console.WriteLine("========[ CREATE BULK INGEST MANIFEST ]========");
            Console.WriteLine("===============================================\n");

            IAsset destAsset1 = objCloudMediaContext.Assets.Create(name + "_asset_1", AssetCreationOptions.None);
            IAsset destAsset2 = objCloudMediaContext.Assets.Create(name + "_asset_2", AssetCreationOptions.None);

            string filename1 = @"C:\Workspace\Destination\Input\MultipleFile\FromRussia\Funny Videos From Russia With Love_937.ismv";
            string filename2 = @"C:\Workspace\Destination\Input\MultipleFile\FromRussia\Funny Videos From Russia With Love_1241.ismv";
            string filename3 = @"C:\Workspace\Destination\Input\MultipleFile\FromRussia\Funny Videos From Russia With Love_230.ismv";

            //Presently, each asset filename uploaded must be unique for an individual Bulk ingest manifest. So two assets can not have the same asset filename or an exception will be thrown for duplicate filename.                                           ===//
            IIngestManifest objIIngestManifest = objCloudMediaContext.IngestManifests.Create(name);
            IIngestManifestAsset bulkAsset1 = objIIngestManifest.IngestManifestAssets.Create(destAsset1, new[] { filename1 });
            IIngestManifestAsset bulkAsset2 = objIIngestManifest.IngestManifestAssets.Create(destAsset2, new[] { filename2, filename3 });

            ListIngestManifests(objIIngestManifest.Id);

            Console.WriteLine("\n===============================================");
            Console.WriteLine("===[ BULK INGEST MANIFEST MONITOR FILE COPY]===");
            Console.WriteLine("===============================================\n");

            UploadBlobFile(objIIngestManifest.BlobStorageUriForUpload, filename1);
            UploadBlobFile(objIIngestManifest.BlobStorageUriForUpload, filename2);
            UploadBlobFile(objIIngestManifest.BlobStorageUriForUpload, filename3);

            MonitorBulkManifest(objIIngestManifest.Id);
            ListIngestManifests(objIIngestManifest.Id);

            return objIIngestManifest;
        }

        /*[1] Need to create only ONE IngestManifest and always use the same for uploading all your files. 
              This will create a manifest blob container that Azure Media Services will be "watching". This container must be the same that the Aspera client uses to upload the files dropped into the on-premise watch folder.
          [2] Both the IIngestManifest and Aspera client will be using the same container, e.g. manifest-c795e5d9-8b51-f347-afca-1079f9ef8db4. You can see it in CloudBerry or others.
              You will have to make sure to create a new IIngestManifestAsset inside the fixed IIngestManifest for each asset file dropped into the watch folder.
          [3] Each asset filename uploaded must be unique for an individual IIngestManifest. 
              So two assets can not have the same asset filename or an exception will be thrown for duplicate filename.
          [4] Instead of using HTTP upload (such as UploadBlobFile method) to upload now and here, we just create the IIngestManifest and 
              use Aspera P2P Client to upload files to the specific container: IIngestManifest.BlobStorageUriForUpload : https://nbcsgvodprodweststor.blob.core.windows.net/manifest-c795e5d9-8b51-f347-afca-1079f9ef8db4
         */
        static IIngestManifest GetIIngestManifest(string manifestName, CloudMediaContext objCloudMediaContext)
        {
            IIngestManifest objIIngestManifest;
            var ingestManifests = objCloudMediaContext.IngestManifests.Where(m => m.Name == manifestName);
            if (ingestManifests.Count() > 0)
            {
                objIIngestManifest = ingestManifests.FirstOrDefault();
            }
            else
            {
                objIIngestManifest = objCloudMediaContext.IngestManifests.Create(manifestName);
            }

            Console.WriteLine("Manifest storage container: IIngestManifest.BlobStorageUriForUpload = {0}", objIIngestManifest.BlobStorageUriForUpload);
            Console.WriteLine("IIngestManifest.Id: {0}", objIIngestManifest.Id);

            //good housekeeping to delete IIngestManifestAsset after job is done
            DeleteBulkManifestAssets(objIIngestManifest.Id);

            return objIIngestManifest;
        }

        static IIngestManifest BulkIngestSingleAsset(string manifestName, string[] assetFilePaths, out IAsset ingestedAsset)
        {
            Console.WriteLine("========[ CREATE BULK INGEST MANIFEST ]========");

            IAsset objIAsset = objCloudMediaContext.Assets.Create(manifestName + "_Asset", AssetCreationOptions.None);

            //Presently, each asset filename uploaded must be unique for an individual Bulk ingest manifest. So two assets can not have the same asset filename or an exception will be thrown for duplicate filename.                                           ===//
            IIngestManifest objIIngestManifest = GetIIngestManifest(manifestName, objCloudMediaContext);
            
            IIngestManifestAsset objIIngestManifestAsset = objIIngestManifest.IngestManifestAssets.Create(objIAsset, assetFilePaths);

            ListIngestManifests(objIIngestManifest.Id);

            Console.WriteLine("===[ BULK INGEST MANIFEST MONITOR FILE COPY]===");
            foreach (string assetFilePath in assetFilePaths)
            {
                Console.WriteLine("objIIngestManifest.BlobStorageUriForUpload = {0}", objIIngestManifest.BlobStorageUriForUpload);
                //instead of using HTTP upload, not upload now and here, instead use Aspera P2P Client to upload files to the specific container: IIngestManifest.BlobStorageUriForUpload : https://nbcsgvodprodweststor.blob.core.windows.net/manifest-c795e5d9-8b51-f347-afca-1079f9ef8db4
                //UploadBlobFile(objIIngestManifest.BlobStorageUriForUpload, assetFilePath);
            }

            MonitorBulkManifest(objIIngestManifest.Id);
            ListIngestManifests(objIIngestManifest.Id);

            ingestedAsset = objIIngestManifestAsset.Asset;

            return objIIngestManifest;
        }

        //Windows Azure Storage Client 2.0 breaking changes: http://blogs.msdn.com/b/windowsazurestorage/archive/2012/10/29/windows-azure-storage-client-library-2-0-breaking-changes-amp-migration-guide.aspx
        static void UploadBlobFile(string destBlobURI, string filename)
        {
            Task copytask = new Task(() =>
            {
                string storageAccountName = System.Configuration.ConfigurationManager.AppSettings["WamsStorageAccountName"];
                string storageAccountKey  = System.Configuration.ConfigurationManager.AppSettings["WamsStorageAccountKey"];
                StorageCredentials accountAndKey = new StorageCredentials(storageAccountName, storageAccountKey);
                var storageaccount = new CloudStorageAccount(accountAndKey, true);
                CloudBlobClient blobClient = storageaccount.CreateCloudBlobClient();
                CloudBlobContainer blobContainer = blobClient.GetContainerReference(destBlobURI);
                
                string[] splitfilename = filename.Split('\\');
                CloudBlockBlob blob = blobContainer.GetBlockBlobReference(splitfilename[splitfilename.Length - 1]);

                //blob.UploadFile(filename);
                blob.UploadFromStream(File.OpenRead(filename));
                lock (consoleWriteLock)
                {
                    Console.WriteLine("Upload for {0} scheduled.", filename);
                }
            });

            copytask.Start();
        }

        static void DeleteBulkManifest(string name)
        {
            Console.WriteLine("\n===============================================");
            Console.WriteLine("=======[ DELETE BULK INGEST MANIFESTS ]========");
            Console.WriteLine("===============================================\n");

            IQueryable<IIngestManifest> objIQueryable_IIngestManifest = objCloudMediaContext.IngestManifests.Where(c => c.Name == name);
            foreach (IIngestManifest objIIngestManifest in objIQueryable_IIngestManifest)
            {
                DeleteBulkManifestAssets(objIIngestManifest.Id);

                Console.WriteLine("Deleting Manifest...\n\tName : {0}\n\tManifest ID : {1}...", objIIngestManifest.Name, objIIngestManifest.Id);
                objIIngestManifest.Delete();
                Console.WriteLine("Delete Complete.\n");
            }
        }

        static void DeleteBulkManifestAssets(string manifestID)
        {
            Console.WriteLine("\n===============================================");
            Console.WriteLine("=====[ DELETE BULK INGEST MANIFEST ASSETS ]====");
            Console.WriteLine("===============================================\n");

            foreach (IIngestManifest objIIngestManifest in objCloudMediaContext.IngestManifests.Where(c => c.Id == manifestID))
            {
                Console.WriteLine("Deleting assets for manifest named : {0}...\n", objIIngestManifest.Name);
                foreach (IIngestManifestAsset objIIngestManifestAsset in objIIngestManifest.IngestManifestAssets)
                {
                    try
                    {
                        foreach (ILocator objILocator in objIIngestManifestAsset.Asset.Locators)
                        {
                            Console.WriteLine("Deleting locator {0} for asset {1}", objILocator.Path, objIIngestManifestAsset.Asset.Id);
                            objILocator.Delete();
                        }
                        Console.WriteLine("Deleting asset {0}\n", objIIngestManifestAsset.Asset.Id);
                        objIIngestManifestAsset.Asset.Delete();
                        objIIngestManifestAsset.Delete();
                    }
                    catch {}
                }
            }
        }

        static void MonitorBulkManifest(string manifestID)
        {
            bool bContinue = true;
            while (bContinue)
            {
                //We need a new context here because IIngestManifestStatistics is considered an expensive property and not updated realtime for a context.                                        ===//
                CloudMediaContext context = GetContext();

                IIngestManifest manifest = context.IngestManifests.Where(m => m.Id == manifestID).FirstOrDefault();

                if (manifest != null)
                {
                    lock (consoleWriteLock)
                    {
                        Console.WriteLine("\nWaiting on all file uploads.");
                        Console.WriteLine("PendingFilesCount  : {0}", manifest.Statistics.PendingFilesCount);
                        Console.WriteLine("FinishedFilesCount : {0}", manifest.Statistics.FinishedFilesCount);
                        Console.WriteLine("{0}% complete.\n", (float)manifest.Statistics.FinishedFilesCount / (float)(manifest.Statistics.FinishedFilesCount + manifest.Statistics.PendingFilesCount) * 100);


                        if (manifest.Statistics.PendingFilesCount == 0)
                        {
                            Console.WriteLine("Completed\n");
                            // Bulk ingest operation complete.
                            // Get the asset from the IIngestManifest.IngestManifestAssets collection and start the transcoding job.
                            IAsset objIAsset = manifest.IngestManifestAssets.FirstOrDefault().Asset;
                            foreach (var assetFile in objIAsset.AssetFiles)
                            {
                                Console.WriteLine("Asset file uploaded: {0}", assetFile.Name);
                            }

                            //good housekeeping to delete IIngestManifestAsset after job is done
                            //DeleteBulkManifestAssets(manifest.Id);

                            bContinue = false;
                        }
                    }

                    if (manifest.Statistics.FinishedFilesCount < manifest.Statistics.PendingFilesCount)
                    {
                        Thread.Sleep(6000);
                    }
                }
                else //=== Manifest is null ===//
                    bContinue = false;
            }
        }

        static IQueryable<IIngestManifest> ListIngestManifests(string manifestId = "")
        {
            CloudMediaContext context = GetContext();

            Console.WriteLine("\n===============================================");
            Console.WriteLine("===========[ LIST BULK INGEST MANIFESTS ]===========");
            Console.WriteLine("===============================================\n");

            IQueryable<IIngestManifest> manifests = null;

            //=== If an Id is supplied, list the manifest with that Id. Otherwise, list all manifests ===//
            if (manifestId == "")
                manifests = context.IngestManifests;
            else
                manifests = context.IngestManifests.Where(m => m.Id == manifestId);

            foreach (IIngestManifest manifest in manifests)
            {
                Console.WriteLine("Manifest Name  : {0}", manifest.Name);
                Console.WriteLine("Manifest State : {0}", manifest.State.ToString());
                Console.WriteLine("Manifest Id    : {0}", manifest.Id);
                Console.WriteLine("Manifest Last Modified      : {0}", manifest.LastModified.ToLocalTime().ToString());
                Console.WriteLine("Manifest PendingFilesCount  : {0}", manifest.Statistics.PendingFilesCount);
                Console.WriteLine("Manifest FinishedFilesCount : {0}", manifest.Statistics.FinishedFilesCount);
                Console.WriteLine("IIngestManifest.BlobStorageUriForUpload : {0}\n", manifest.BlobStorageUriForUpload);

                foreach (IIngestManifestAsset manifestasset in manifest.IngestManifestAssets)
                {
                    //Console.WriteLine("\tAsset Name    : {0}", manifestasset.Asset.Name);
                    //Console.WriteLine("\tAsset ID      : {0}", manifestasset.Asset.Id);
                    //Console.WriteLine("\tAsset Options : {0}", manifestasset.Asset.Options.ToString());
                    //Console.WriteLine("\tAsset State   : {0}", manifestasset.Asset.State.ToString());
                    //Console.WriteLine("\tAsset Files....");

                    foreach (IIngestManifestFile assetfile in manifestasset.IngestManifestFiles)
                    {
                        Console.WriteLine("\t\t{0}\n\t\tFile State : {1}\n", assetfile.Name, assetfile.State.ToString());
                    }
                    Console.WriteLine("");
                }
            }

            return manifests;
        }
        #endregion

        #region Process

        static ITask AddTaskToJob(IJob objIJob, string mediaProcessorName, string taskName, IAsset inputAsset, string configFilePathOrString, string outputAssetName, TaskOptions objTaskOptions, AssetCreationOptions outputAssetCreationOptions, out IAsset outputAsset)
        {
            //read config string depending on which processor used: from file, string or nothing
            string taskConfig = string.Empty;
            IAsset objIAssetGraph = null;  //used only in the case of Kayak
            switch (mediaProcessorName)
            {
                case "Storage Decryption":
                    taskConfig = string.Empty;
                    break;
                case "Windows Azure Media Encoder":
                case "Azure Media Encoder":
                    if (configFilePathOrString.EndsWith(".xml"))   //preset file
                        taskConfig = File.ReadAllText(Path.GetFullPath(configFilePathOrString));
                    else
                        taskConfig = configFilePathOrString;       //or perset string
                    break;
                case "Windows Azure Media Packager":
                case "Windows Azure Media Encryptor":
                case "Windows Azure Media Indexer":
                case "Azure Media Indexer":
                    taskConfig = File.ReadAllText(Path.GetFullPath(configFilePathOrString));
                    break;
                case "Digital Rapids - Kayak Cloud Engine":
                    taskConfig = configFilePathOrString; //some configuration e.g. 216.191.138.100,15000,,.mp4
                    //create IAsset for graph file
                    objIAssetGraph = UploadGraph("graphAsset");
                    break;
                default:
                    break;
            }

            // Get a media processor reference, and pass to it the name of the processor to use for the specific task.
            IMediaProcessor objIMediaProcessor = GetLatestMediaProcessorByName(mediaProcessorName);

            // Create a task with the conversion details, using the configuration data. 
            ITask objITask = objIJob.Tasks.AddNew(taskName, objIMediaProcessor, taskConfig, objTaskOptions);
            //if Digital Rapid Kayak MP, add graph file [ graph file must be added before asset]
            if (objIAssetGraph != null)
            {
                objITask.InputAssets.Add(objIAssetGraph);
            }
            // Specify the input asset to be converted.
            objITask.InputAssets.Add(inputAsset);

            // Add an output asset to contain the results of the job. From John Deutscher: There is no longer a concept of “temporary” Assets as there was in the Preview version of WAMS. You should manage all intermediate Assets that you no longer wish to use in your own code.
            objITask.OutputAssets.AddNew(outputAssetName, outputAssetCreationOptions);

            outputAsset = objITask.OutputAssets[0];
            //outputAsset = GetAssetFromJobAndAssetName(objIJob.Id, outputAssetName);

            return objITask;
        }

        static ITask AddTaskToJob(IJob objIJob, string mediaProcessorName, string taskName, IAsset[] inputAssets, string configFilePathOrString, string outputAssetName, TaskOptions objTaskOptions, AssetCreationOptions outputAssetCreationOptions, out IAsset outputAsset)
        {
            //read config string depending on which processor used: from file, string or nothing
            string taskConfig = string.Empty;
            IAsset objIAssetGraph = null;  //used only in the case of Kayak
            switch (mediaProcessorName)
            {
                case "Storage Decryption":
                    taskConfig = string.Empty;
                    break;
                case "Windows Azure Media Encoder":
                case "Azure Media Encoder":
                    if (configFilePathOrString.EndsWith(".xml"))   //preset file
                        taskConfig = File.ReadAllText(Path.GetFullPath(configFilePathOrString));
                    else
                        taskConfig = configFilePathOrString;       //or perset string
                    break;
                case "Windows Azure Media Packager":
                case "Windows Azure Media Encryptor":
                case "Windows Azure Media Indexer":
                case "Azure Media Indexer":
                    taskConfig = File.ReadAllText(Path.GetFullPath(configFilePathOrString));
                    break;
                case "Digital Rapids - Kayak Cloud Engine":
                    taskConfig = configFilePathOrString; //some configuration e.g. 216.191.138.100,15000,,.mp4
                    //create IAsset for graph file
                    objIAssetGraph = UploadGraph("graphAsset");
                    break;
                default:
                    break;
            }

            // Get a media processor reference, and pass to it the name of the processor to use for the specific task.
            IMediaProcessor objIMediaProcessor = GetLatestMediaProcessorByName(mediaProcessorName);

            // Create a task with the conversion details, using the configuration data. 
            ITask objITask = objIJob.Tasks.AddNew(taskName, objIMediaProcessor, taskConfig, objTaskOptions);
            //if Digital Rapid Kayak MP, add graph file [ graph file must be added before asset]
            if (objIAssetGraph != null)
            {
                objITask.InputAssets.Add(objIAssetGraph);
            }
            // Specify the input asset to be converted.
            for (int i = 0; i < inputAssets.Length; i++)
            {
                objITask.InputAssets.Add(inputAssets[i]);  //order is critical for stitching video clips
            }

            // Add an output asset to contain the results of the job. From John Deutscher: There is no longer a concept of “temporary” Assets as there was in the Preview version of WAMS. You should manage all intermediate Assets that you no longer wish to use in your own code.
            objITask.OutputAssets.AddNew(outputAssetName, outputAssetCreationOptions);

            outputAsset = objITask.OutputAssets[0];
            //outputAsset = GetAssetFromJobAndAssetName(objIJob.Id, outputAssetName);

            return objITask;
        }


        private static IAsset UploadGraph(string assetName)
        {
            //create IAsset for graph file
            string filename     = System.Configuration.ConfigurationManager.AppSettings["KayakBlueprint"];
            string fullFilePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\..\..\";
            fullFilePath = Path.GetFullPath(fullFilePath + filename);
            filename     = Path.GetFileName(fullFilePath);
            IAsset objIAssetGraph = objCloudMediaContext.Assets.Create(assetName, AssetCreationOptions.None);
            IAssetFile objIAssetFile = objIAssetGraph.AssetFiles.Create(filename);
            objIAssetFile.Upload(fullFilePath);

            return objIAssetGraph;
        }

        private static void ListAllMediaProcessors()
        {
            string FORMAT = "{0,-40}{1,-20}{2,-20}{3,-70}{4,-50}{5,-20}";
            Console.WriteLine(FORMAT, "Name", "Version", "Vendor", "Description", "Id", "Sku");
            var processors = objCloudMediaContext.MediaProcessors.OrderBy(p => p.Name);
            foreach(IMediaProcessor objIMediaProcessor in processors )
            {
                Console.WriteLine(FORMAT, objIMediaProcessor.Name, objIMediaProcessor.Version, objIMediaProcessor.Vendor, objIMediaProcessor.Description, objIMediaProcessor.Id, objIMediaProcessor.Sku);
            }
        }

        //Media Processor
        private static IMediaProcessor GetLatestMediaProcessorByName(string mediaProcessorName)
        {
            var processor = objCloudMediaContext.MediaProcessors.Where(p => p.Name == mediaProcessorName).ToList().OrderBy(p => new Version(p.Version)).LastOrDefault();

            Console.WriteLine("Media Processor ID selected: {0}", processor.Id);

            if (processor == null)
                throw new ArgumentException(string.Format("Unknown media processor", mediaProcessorName));

            return processor;
        }

        //Job progress
        private static void StateChanged(object sender, JobStateChangedEventArgs e)
        {
            Console.WriteLine("Job StateChanged event:");
            Console.WriteLine("  Previous state: " + e.PreviousState);
            Console.WriteLine("  Current state: " + e.CurrentState);

            switch (e.CurrentState)
            {
                case JobState.Finished:
                    Console.WriteLine("Job is finished.");
                    break;
                case JobState.Canceling:
                case JobState.Queued:
                case JobState.Scheduled:
                case JobState.Processing:
                    Console.WriteLine("Please wait...\n");
                    break;
                case JobState.Canceled:
                case JobState.Error:
                    break;
                default:
                    break;
            }

            // Cast sender as a job.
            IJob job = (IJob)sender;
            // Display or log error details as needed.
            LogJobStop(job.Id);
        }

        private static void LogJobStop(string jobId)
        {
            StringBuilder builder = new StringBuilder();
            IJob job = objCloudMediaContext.Jobs.Where<IJob>(j => j.Id == jobId).FirstOrDefault<IJob>();

            //builder.AppendLine("\nThe job stopped due to cancellation or an error.");
            builder.AppendLine("***************************");
            builder.AppendLine("Job ID: " + job.Id);
            builder.AppendLine("Job Name: " + job.Name);
            builder.AppendLine("Job State: " + job.State.ToString());
            builder.AppendLine("Job started (server UTC time): " + job.StartTime.ToString());
            //builder.AppendLine("Media Services account name: " + _accountName);
            //builder.AppendLine("Media Services account location: " + _accountLocation);
            // Log job errors if they exist.  
            if (job.State == JobState.Error)
            {
                builder.Append("Error Details: \n");
                foreach (ITask task in job.Tasks)
                {
                    foreach (ErrorDetail detail in task.ErrorDetails)
                    {
                        builder.AppendLine("  Task Id: " + task.Id);
                        builder.AppendLine("    Error Code: " + detail.Code);
                        builder.AppendLine("    Error Message: " + detail.Message + "\n");
                    }
                }
            }
            builder.AppendLine("***************************\n");

            // Write the output to a local file and to the console. The template for an error output file is:  JobStop-{JobId}.txt
            /*
            string _outputFilesFolder = @"C:\Workspace\Destination\Output";
            string outputFile = _outputFilesFolder + @"\JobStop-" + JobIdAsFileName(job.Id) + ".txt";
            WriteToFile(builder.ToString(), outputFile, Encoding.ASCII);
            */
            Console.Write(builder.ToString());
        }

        static void MonitorTaskProgress(IJob objIJob, int pollInterval)
        {
            CloudMediaContext objCloudMediaContext1;
            while ((objIJob.State != JobState.Finished) && (objIJob.State != JobState.Canceled) && (objIJob.State != JobState.Error))
            {
                foreach (ITask objITask in objIJob.Tasks)
                {
                    Console.WriteLine("Task {0} percent complete: {1}", objITask.Name, objITask.Progress);
                }

                Thread.Sleep(TimeSpan.FromSeconds(pollInterval));
                objCloudMediaContext1 = GetContext();
                objIJob = objCloudMediaContext1.Jobs.Where(j => j.Id == objIJob.Id).Single();
            }

        }

        //A simple job with a single task
        static IJob CreateSimpleJob(string inputMediaFilePath)
        {
            bool download = true;
            string assetToDownload = "MP4 Asset";

            //Create an encrypted asset and upload to storage. 
            IAsset asset = CreateAssetAndUploadSingleFile(AssetCreationOptions.None, inputMediaFilePath);

            // Declare a new job.
            IJob job = objCloudMediaContext.Jobs.Create("My simple job");

            IAsset inputAsset = asset;
            IAsset outputAsset;
            string configFilePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\..\..";
            //AddTaskToJob(job, "Windows Azure Media Encoder", "H264 Encoding Task", inputAsset, "H264 Adaptive Bitrate MP4 Set 720p", "H264 Encoded Asset", TaskOptions.None, AssetCreationOptions.None, out outputAsset);
            //AddTaskToJob(job, "Windows Azure Media Encoder", "H264 Encoding Task", inputAsset, configFilePath + @"\EncoderPresetFromEE4ProSP2.xml", "H264 EE4ProSP2 Encoded Asset", TaskOptions.None, AssetCreationOptions.None, out outputAsset);
            //AddTaskToJob(job, "Windows Azure Media Encoder", "Generate Thumbnails Task", inputAsset, configFilePath + @"\EncoderPreset_Thumbnail.xml", "Thumbnails Asset", TaskOptions.None, AssetCreationOptions.None, out outputAsset);
            AddTaskToJob(job, "Windows Azure Media Encoder", "WMV to MP4 Task", inputAsset, "H264 Broadband SD 16x9", "MP4 Asset", TaskOptions.None, AssetCreationOptions.None, out outputAsset);

            // Use the following event handler to check job progress.  
            job.StateChanged += new EventHandler<JobStateChangedEventArgs>(StateChanged);

            // Launch the job.
            job.Submit();

            // Optionally log job details. This displays basic job details
            // to the console and saves them to a JobDetails-{JobId}.txt file 
            // in your output folder.
            //LogJobStop(job.Id);  //to avoid confusion, comment this logging. Let logging happen only in StateChanged

            MonitorTaskProgress(job, 10);

            // Check job execution and wait for job to finish. 
            Task progressJobTask = job.GetExecutionProgressTask(CancellationToken.None);
            progressJobTask.Wait();

            // If job state is Error, the event handling 
            // method for job progress should log errors.  Here we check 
            // for error state and exit if needed.
            if (job.State == JobState.Error)
            {
                Console.WriteLine("\nExiting method due to job error.");
                return job;
            }
            // Perform other tasks. For example, access the assets that are the output of a job, 
            // either by creating URLs to the asset on the server, or by downloading.
            IAsset usableAsset;  //the outputAsset fails with exception: function call to member IAsset.Id is not supported
            
            //download an output asset
            if (download)
            {
                usableAsset = GetAssetFromJobAndAssetName(job.Id, assetToDownload);
                DownloadAsset(usableAsset.Id, @"C:\Workspace\Destination\Output");
            }

            return job;
        }



        public static IJob CreatePlayReadyProtectionJob(string inputMediaFilePath, /*string primaryFilePath, */string configFilePath)
        {
            // Create a storage-encrypted asset and upload the mp4. 
            //IAsset asset = CreateAssetAndUploadSingleFile(AssetCreationOptions.StorageEncrypted, inputMediaFilePath);
            IAsset asset = CreateAssetAndUploadMultipleFiles("TestAsset", AssetCreationOptions.StorageEncrypted, inputMediaFilePath);

            // Declare a new job to contain the tasks.
            IJob job = objCloudMediaContext.Jobs.Create("My PlayReady Protection job");

            //2 tasks:
            IAsset inputAsset, outputAsset;
            inputAsset = asset;
            AddTaskToJob(job, "Windows Azure Media Packager",  "MP4 Preprocessor",          inputAsset, configFilePath + @"\MediaPackager_ValidateTask.xml",          "MP4 Valdated Asset",                TaskOptions.None,                   AssetCreationOptions.None,                      out outputAsset);
            inputAsset = outputAsset;
            AddTaskToJob(job, "Windows Azure Media Packager",  "MP4 to Smooth Task",        inputAsset, configFilePath + @"\MediaPackager_MP4ToSmooth.xml",           "Smooth Streaming Output Asset",     TaskOptions.None,                   AssetCreationOptions.None,                      out outputAsset);
            inputAsset = outputAsset;
            AddTaskToJob(job, "Windows Azure Media Encryptor", "PlayReady Protection Task", inputAsset, configFilePath + @"\MediaEncryptor_PlayReadyProtection.xml",  "PlayReady Protected Smooth Asset",  TaskOptions.ProtectedConfiguration, AssetCreationOptions.CommonEncryptionProtected, out outputAsset);

            /*
            //TASK 1: 
            // Read the task configuration data into a string. 
            string configMp4ToSmooth = File.ReadAllText(Path.GetFullPath(configFilePath + @"\MediaPackager_MP4ToSmooth.xml"));

            // Get a media processor reference, and pass to it the name of the processor to use for the specific task.
            IMediaProcessor processor = GetLatestMediaProcessorByName("Windows Azure Media Packager");

            // Create a task with the conversion details, using the configuration data. 
            ITask task = job.Tasks.AddNew("My Mp4 to Smooth Task",
                processor,
                configMp4ToSmooth,
                TaskOptions.None);
            // Specify the input asset to be converted.
            task.InputAssets.Add(asset);
            // Add an output asset to contain the results of the job. We do not need to persist the output asset to storage, so set the shouldPersistOutputOnCompletion param to false. 
            task.OutputAssets.AddNew("Streaming output asset", AssetCreationOptions.None);

            IAsset smoothOutputAsset = task.OutputAssets[0];

            //TASK 2
            // Read the configuration data into a string. 
            string configPlayReady = File.ReadAllText(Path.GetFullPath(configFilePath + @"\MediaEncryptor_PlayReadyProtection2.xml"));

            // Get a media processor reference, and pass to it the name of the processor to use for the specific task.
            IMediaProcessor playreadyProcessor = GetLatestMediaProcessorByName("Windows Azure Media Encryptor");

            // Create a second task. 
            ITask playreadyTask = job.Tasks.AddNew("My PlayReady Task",
                playreadyProcessor,
                configPlayReady,
                TaskOptions.ProtectedConfiguration);
            // Add the input asset, which is the smooth streaming output asset from the first task. 
            playreadyTask.InputAssets.Add(smoothOutputAsset);
            // Add an output asset to contain the results of the job. Persist the output by setting the shouldPersistOutputOnCompletion param to true.
            playreadyTask.OutputAssets.AddNew("PlayReady protected output asset", AssetCreationOptions.CommonEncryptionProtected);
            */

            // Use the following event handler to check job progress. 
            job.StateChanged += new EventHandler<JobStateChangedEventArgs>(StateChanged);
            // Launch the job.
            job.Submit();

            // Optionally log job details. This displays basic job details to the console and saves them to a JobDetails-{JobId}.txt file in your output folder.
            LogJobStop(job.Id);

            MonitorTaskProgress(job, 10);

            // Check job execution and wait for job to finish. 
            Task progressJobTask = job.GetExecutionProgressTask(CancellationToken.None);
            progressJobTask.Wait();

            // If job state is Error, the event handling method for job progress should log errors.  Here we check for error state and exit if needed.
            if (job.State == JobState.Error)
            {
                Console.WriteLine("\nExiting method due to job error.");
                return job;
            }
            // Perform other tasks. For example, access the assets that are the output of a job, 
            // either by creating URLs to the asset on the server, or by downloading. 

            return job;
        }


        #endregion

        #region Manage

        static void ListAssets()
        {
            string waitMessage = "Building the list. This may take a few seconds to a few minutes depending on how many assets you have."
                + Environment.NewLine + Environment.NewLine + "Please wait..." + Environment.NewLine;
            Console.Write(waitMessage);

            // Create a Stringbuilder to store the list that we build. 
            StringBuilder builder = new StringBuilder();
            var assets = objCloudMediaContext.Assets.OrderBy(a => a.LastModified);

            foreach (IAsset asset in assets)
            {
                //if (asset.Name.Contains("min"))
                //{
                    // Display the collection of assets.
                    builder.AppendLine("");
                    builder.AppendLine("******ASSET******");
                    builder.AppendLine("Asset ID: " + asset.Id);
                    builder.AppendLine("Name: " + asset.Name);
                    builder.AppendLine("AssetCreationOptions: " + asset.Options.ToString());
                    builder.AppendLine("==============");
                    builder.AppendLine("******ASSET FILES******");

                    // Display the files associated with each asset. 
                    foreach (IAssetFile fileItem in asset.AssetFiles)
                    {
                        builder.AppendLine("File Name: " + fileItem.Name);
                        builder.AppendLine("Size: " + fileItem.ContentFileSize);
                        builder.AppendLine("==============");
                    }
                //}
            }

            builder.AppendLine("End of ListAssets()");
            // Display output in console.
            Console.Write(builder.ToString());
        }

        //delete all assets except those specified or published
        static void DeleteAllAssets(string[] exceptionAssetNames)
        {
            List<string> objList_string = new List<string>();
            foreach (string assetName in exceptionAssetNames)
            {
                objList_string.Add(assetName);
            }
            foreach (IAsset objIAsset in objCloudMediaContext.Assets)
            {
                if (!objList_string.Contains(objIAsset.Name)/* && objIAsset.Locators.Count == 0*/)
                {
                    objIAsset.Delete();
                }
            }
            Console.WriteLine("DeleteAllAssets() completed.");
        }

        static IAsset GetAsset(string assetId)
        {
            // Use a LINQ Select query to get an asset.
            var assetInstance = from a in objCloudMediaContext.Assets where a.Id == assetId select a;
            // Reference the asset as an IAsset.
            IAsset asset = assetInstance.FirstOrDefault();
            return asset;
        }

        //sorted by IJob.StartTime
        static void ListAllJobs()
        {
            JobBaseCollection objJobBaseCollection = objCloudMediaContext.Jobs;   //directly sorting does not work
            //retrieve and store to local before sorting
            List<IJob> objList_IJob = new List<IJob>();
            foreach (IJob objIJob1 in objJobBaseCollection)
            {
                objList_IJob.Add(objIJob1);
            }
            IJob[] objIJobs = objList_IJob.ToArray().OrderBy(job => job.StartTime).ToArray<IJob>();

            foreach (IJob objIJob in objIJobs)
            {
                Console.WriteLine(string.Format("JobId = {0}, Name = {1}, StartTime={2}", objIJob.Id, objIJob.Name, objIJob.StartTime.Value.ToString("MM-dd-yyyy HH:mm:ss")));
                //objIJob.Delete();
            }
        }

        static void DeleteJobs(int numberOfOldestJobs)
        {
            JobBaseCollection objJobBaseCollection = objCloudMediaContext.Jobs;   //directly sorting does not work
            //retrieve and store to local before sorting
            List<IJob> objList_IJob = new List<IJob>();
            foreach (IJob objIJob1 in objJobBaseCollection)
            {
                objList_IJob.Add(objIJob1);
            }
            IJob[] objIJobs = objList_IJob.ToArray().OrderBy(job => job.StartTime).ToArray<IJob>();

            //delete the oldest few
            if (numberOfOldestJobs > 0)
            {
                for (int i = 0; i < (int)Math.Min(numberOfOldestJobs, objIJobs.Length); i++)
                {
                    Console.WriteLine(string.Format("Deleting: JobId = {0}, Name = {1}, StartTime={2}", objIJobs[i].Id, objIJobs[i].Name, objIJobs[i].StartTime.Value.ToString("MM-dd-yyyy HH:mm:ss")));
                    objIJobs[i].Delete();
                }
            }

            Console.WriteLine("DeleteJobs() completed.");
        }

        static IJob GetJob(string jobId)
        {
            IJob objIJob = objCloudMediaContext.Jobs.Where<IJob>(j => j.Id == jobId).FirstOrDefault<IJob>();

            return objIJob;
        }

        /*
        GetAsset: Asset ID = nb:cid:UUID:c707b143-41ab-4a4f-a553-188b06b3bf86
        Streaming asset base path on origin:
        http://willzhanmediaservice.origin.mediaservices.windows.net/e0ffebd1-21c0-43a7-a581-8baf8e9c4c23/
        URL to manifest for client streaming:
        http://willzhanmediaservice.origin.mediaservices.windows.net/e0ffebd1-21c0-43a7-a581-8baf8e9c4c23/Anna.ism/manifest

        Origin locator Id:  nb:lid:UUID:e0ffebd1-21c0-43a7-a581-8baf8e9c4c23
        Access policy Id:   nb:pid:UUID:64ee1e1e-7e54-41a6-8a59-c24f6540d513
        Streaming asset Id: nb:cid:UUID:c707b143-41ab-4a4f-a553-188b06b3bf86
        */
        public static ILocator GetStreamingOriginLocator(string assetId, MediaContentType objMediaContentType, bool createNew)
        {
            // Get a reference to the asset you want to stream.
            IAsset objIAsset = GetAsset(assetId);

            // Get a reference to the streaming manifest file from the collection of files in the asset
            IAssetFile objIAssetFile = null; 
            switch (objMediaContentType)
            {
                case MediaContentType.SmoothStreaming:
                case MediaContentType.HLS:
                    var theManifest = objIAsset.AssetFiles.Where(f => f.Name.EndsWith(".ism"));
                    objIAssetFile = theManifest.First();
                    break;
                case MediaContentType.ProgressiveDownload:
                    var videoFiles = objIAsset.AssetFiles.Where(f => f.Name.EndsWith(".mp4") || f.Name.EndsWith(".wmv"));
                    objIAssetFile = videoFiles.FirstOrDefault();
                    break;
                default:
                    break;
            }

            // Create a 2000-day readonly access policy. 
            IAccessPolicy objIAccessPolicy = objCloudMediaContext.AccessPolicies.Create("30d Streaming policy", TimeSpan.FromDays(1000), AccessPermissions.Read);

            ILocator objILocator = null;
            // Create a locator to the streaming content on an origin. 
            if (createNew)
                objILocator = objCloudMediaContext.Locators.CreateLocator(LocatorType.OnDemandOrigin, objIAsset, objIAccessPolicy, DateTime.UtcNow.AddMinutes(-5));
            else
            {
                //or use an existing origin locator
                //var theLocator = from l in objCloudMediaContext.Locators where l.Path.Contains("origin.mediaservices.windows.net") select l;
                var locators = objCloudMediaContext.Locators.Where(l => l.Type == LocatorType.OnDemandOrigin);
                objILocator = locators.FirstOrDefault();
            }

            string url = string.Empty; 
            switch (objMediaContentType)
            {
                case MediaContentType.SmoothStreaming:
                    url = objILocator.Path + objIAssetFile.Name + "/manifest";
                    break;
                case MediaContentType.HLS:
                    url = objILocator.Path + objIAssetFile.Name + "/manifest(format=m3u8-aapl)";
                    break;
                case MediaContentType.ProgressiveDownload:
                    url = objILocator.Path + objIAssetFile.Name;
                    break;
                default:
                    break;
            }
            
            //display
            Console.WriteLine(string.Format("Origin URL: {0}", url));
            Console.WriteLine(string.Format("Streaming asset base path on origin: {0}", objILocator.Path));
            Console.WriteLine("Origin locator Id: " + objILocator.Id);
            Console.WriteLine("Access policy Id: " + objIAccessPolicy.Id);
            Console.WriteLine("Streaming asset Id: " + objIAsset.Id);

            // Return the locator. 
            return objILocator;
        }

        public enum MediaContentType
        {
            SmoothStreaming,
            HLS,
            ProgressiveDownload
        }
        
        //ILocator.Path=http://willzhanmediaservice.origin.mediaservices.windows.net/9243aca8-2963-435f-92bd-346a09dac989/
        //Publish URL  =http://willzhanmediaservice.origin.mediaservices.windows.net/9243aca8-2963-435f-92bd-346a09dac989/ElephantsDream.ism/Manifest
        public static void ListILocators()
        {
            foreach (ILocator objILocator in objCloudMediaContext.Locators)
            {
                Console.WriteLine(string.Format("Asset.Name = {0}, Id = {1}, Path = {2}", objILocator.Asset.Name, objILocator.Id, objILocator.Path));
            }
        }

        public static void ListILocators(string assetId)
        {
            var locators = objCloudMediaContext.Locators.Where(l => l.AssetId == assetId);
            foreach (ILocator objILocator in locators)
            {
                Console.WriteLine(string.Format("Asset.Name = {0}, Id = {1}, Path = {2}", objILocator.Asset.Name, objILocator.Id, objILocator.Path));
            }
        }

        public static void RemoveILocators(string assetId)
        {
            var locators = objCloudMediaContext.Locators.Where(l => l.AssetId == assetId);
            foreach (ILocator objILocator in locators)
            {
                Console.WriteLine(string.Format("Removing locator for Asset.Name = {0}, Id = {1}, Path = {2}", objILocator.Asset.Name, objILocator.Id, objILocator.Path));
                objILocator.Delete();
            }
        }

        #endregion

        #region Deliver

        static IAsset DownloadAsset(string assetId, string outputFolder)
        {
            IAsset objIAsset = GetAsset(assetId);

            IAccessPolicy accessPolicy = objCloudMediaContext.AccessPolicies.Create("File Download Policy", TimeSpan.FromDays(30), AccessPermissions.Read);
            ILocator locator;
            //if (objIAsset.Locators.Count<ILocator>() < 5) //each asset cannot have more than 5 locators
            //{
            locator = objCloudMediaContext.Locators.CreateSasLocator(objIAsset, accessPolicy);
            //}
            //else
            //{
            //    locator = objCloudMediaContext.Locators.FirstOrDefault();
            //}
            BlobTransferClient blobTransfer = new BlobTransferClient
            {
                NumberOfConcurrentTransfers = 20,
                ParallelTransferThreadCount = 20
            };

            var downloadTasks = new List<Task>();
            foreach (IAssetFile outputFile in objIAsset.AssetFiles)
            {
                // Use the following event handler to check download progress.
                outputFile.DownloadProgressChanged += DownloadProgress;

                string localDownloadPath = Path.Combine(outputFolder, outputFile.Name);
                Console.WriteLine("File download path:  " + localDownloadPath);

                //does not work due to mismatch with v 1.7.1
                downloadTasks.Add(outputFile.DownloadAsync(Path.GetFullPath(localDownloadPath), blobTransfer, locator, CancellationToken.None));

                outputFile.DownloadProgressChanged -= DownloadProgress;
            }

            Task.WaitAll(downloadTasks.ToArray());

            return objIAsset;
        }

        static void DownloadProgress(object sender, Microsoft.WindowsAzure.MediaServices.Client.DownloadProgressChangedEventArgs e)
        {
            Console.WriteLine(string.Format("Asset File:{0}  {1}% download progress. ", ((IAssetFile)sender).Name, e.Progress));
        }

        #endregion

        #region Workflow/Job

        //Complete workflow: Bulk ingest, generate thumbnails, H264 encode, storage decryption, MP4 preprocess, smooth package, DRM protect, locator publish, file download, clean up
        //Encoding task preset strings: http://msdn.microsoft.com/en-us/library/jj129582.aspx
        public static IJob DoWorkflow(IAsset objIAssetIn, string outputFolder, out IAsset objIAssetOut)
        {
            //parameters to customize
            string jobName         = "Workflow job";
            string configFilePath  = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\..\..";
            bool publish           = false;                                //whether to publish (the final) output asset
            string assetToPublish  = "Smooth Streaming Output Asset";     //"PlayReady Protection Output Asset" "Smooth Streaming Output Asset"
            bool download          = false;                               //whether to download an IAsset
            string assetToDownload = "PlayReady Protected HLS Asset";     //"Thumbnails Asset" "Smooth Streaming Output Asset" "HLS Output Asset"
            bool deleteIntermediateAssets = false;                         //whether to delete intermediate assets

            //variables
            IAsset inputAsset, outputAsset;
            IJob objIJob;
            
            // Declare a new job.
            objIJob = objCloudMediaContext.Jobs.Create(jobName);

            //add tasks to the job (using preset string or preset file for WAME)
            inputAsset = objIAssetIn;
            //AddTaskToJob(objIJob, "Windows Azure Media Encoder",   "Generate Thumbnails Task",  inputAsset, "Thumbnails",                                                 "Thumbnails Asset",                  TaskOptions.None,                   AssetCreationOptions.None,                      out outputAsset);
            //AddTaskToJob(objIJob, "Windows Azure Media Encoder",   "Generate Thumbnails Task",  inputAsset, configFilePath + @"\EncoderPreset_Thumbnail.xml",              "Thumbnails Asset",                  TaskOptions.None,                    AssetCreationOptions.None,                      out outputAsset);
            //AddTaskToJob(objIJob, "Windows Azure Media Encoder",   "H264 Encoding Task",        inputAsset, "H264 Adaptive Bitrate MP4 Set 720p",                         "H264 Encoded Asset",                TaskOptions.ProtectedConfiguration, AssetCreationOptions.StorageEncrypted,          out outputAsset);
            //AddTaskToJob(objIJob, "Windows Azure Media Encoder",   "H264 Encoding Task",        inputAsset, configFilePath + @"\EncoderPresetFromEE4ProSP2.xml",          "H264 EE4ProSP2 Encoded Asset",      TaskOptions.ProtectedConfiguration, AssetCreationOptions.StorageEncrypted,          out outputAsset);
            //AddTaskToJob(objIJob, "Windows Azure Media Encoder",   "H264 Encoding Task",        inputAsset, configFilePath + @"\EncoderPreset_MBR_MP4_NBCSG.xml",           "MBR fMP4 Asset",                    TaskOptions.None,                   AssetCreationOptions.StorageEncrypted,          out outputAsset);
            //AddTaskToJob(objIJob, "Azure Media Encoder", "H264 Encoding Task", inputAsset, configFilePath + @"\EncoderPreset_MBR_MP4_NBCSG_7Bitrates.xml", "H264 EE4ProSP2 Encoded Asset", TaskOptions.None, AssetCreationOptions.None, out outputAsset);
            AddTaskToJob(objIJob, "Azure Media Encoder",   "H264 Encoding Task",        inputAsset, configFilePath + @"\customFormat2.xml", "H264 EE4ProSP2 Encoded Asset",     TaskOptions.None,                   AssetCreationOptions.None,                      out outputAsset);
            //inputAsset = outputAsset;
            //AddTaskToJob(objIJob, "Storage Decryption",            "Storage Decryption Task",   inputAsset, string.Empty,                                                  "Storage Decrypted Asset",           TaskOptions.None,                    AssetCreationOptions.None,                      out outputAsset);
            inputAsset = outputAsset;
            AddTaskToJob(objIJob, "Windows Azure Media Packager", "MP4 Preprocessor Task",      inputAsset, configFilePath + @"\MediaPackager_ValidateTask.xml",            "MP4 Valdated Asset",                TaskOptions.None,                   AssetCreationOptions.None,                      out outputAsset);
            inputAsset = outputAsset;
            AddTaskToJob(objIJob, "Windows Azure Media Packager", "MP4 to Smooth Task",         inputAsset, configFilePath + @"\MediaPackager_MP4ToSmooth.xml",             "Smooth Streaming Output Asset",     TaskOptions.None,                   AssetCreationOptions.None,                      out outputAsset);
            //inputAsset = outputAsset;
            ////AddTaskToJob(objIJob, "Windows Azure Media Packager",  "Smooth to HLS Task",        inputAsset, configFilePath + @"\MediaPackager_SmoothToHLS.xml",           "HLS Output Asset",                  TaskOptions.None,                   AssetCreationOptions.None,                      out outputAsset);
            //AddTaskToJob(objIJob, "Windows Azure Media Encryptor", "PlayReady Protection Task", inputAsset, configFilePath + @"\MediaEncryptor_PlayReadyProtection.xml",   "PlayReady Protected Smooth Asset",  TaskOptions.ProtectedConfiguration,  AssetCreationOptions.CommonEncryptionProtected, out outputAsset);
            //inputAsset = outputAsset;
            //AddTaskToJob(objIJob, "Windows Azure Media Packager",  "PR Smooth to PR HLS Task",  inputAsset, configFilePath + @"\MediaPackager_SmoothToHLS.xml",            "PlayReady Protected HLS Asset",     TaskOptions.ProtectedConfiguration,  AssetCreationOptions.None,                      out outputAsset);

            // Use the following event handler to check job progress. 
            objIJob.StateChanged += new EventHandler<JobStateChangedEventArgs>(StateChanged);
            // Launch the job.
            objIJob.Submit();

            // Optionally log job details. This displays basic job details to the console and saves them to a JobDetails-{JobId}.txt file in your output folder.
            LogJobStop(objIJob.Id);

            // Check job execution and wait for job to finish. 
            Task progressJobTask = objIJob.GetExecutionProgressTask(CancellationToken.None);
            progressJobTask.Wait();

            //output final output asset
            objIAssetOut = outputAsset;

            // If job state is Error, the event handling method for job progress should log errors.  Here we check for error state and exit if needed.
            if (objIJob.State == JobState.Error)
            {
                Console.WriteLine("\nExiting method due to job error.");
                return objIJob;
            }

            IAsset usableAsset;  //the outputAsset fails with exception: function call to member IAsset.Id is not supported
            //publish (create ILocator)
            if (publish)
            {
                usableAsset = GetAssetFromJobAndAssetName(objIJob.Id, assetToPublish);
                GetStreamingOriginLocator(usableAsset.Id, MediaContentType.SmoothStreaming, true);
            }

            //download an output asset
            if (download)
            {
                usableAsset = GetAssetFromJobAndAssetName(objIJob.Id, assetToDownload);
                DownloadAsset(usableAsset.Id, outputFolder);
            }

            //delete intermediate assets and ingest manifest
            Console.WriteLine("Hit any key to continue - give a chance to inspect the assets in portal before cleaning up");
            Console.ReadKey();
            
            //delete intermediate task output asset
            if (deleteIntermediateAssets)
            {
                for (int i = 0; i < objIJob.Tasks.Count - 1; i++)
                {
                    objIJob.Tasks[i].OutputAssets[0].Delete();
                }
            }

            return objIJob;
        }

        static IJob HLSWorkflow(IAsset objIAssetIn, string outputFolder, out IAsset objIAssetOut)
        {
            //parameters to customize
            string jobName = "HLS Workflow job";
            string configFilePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\..\..";
            bool publish = false;                                  //whether to publish (the final) output asset
            string assetToPublish = "HLS Output Asset"; 
            bool download = false;                                 //whether to download an IAsset
            string assetToDownload = "Smooth Streaming Output Asset";               
            bool deleteIntermediateAssets = false;                  //whether to delete intermediate assets

            //variables
            IAsset inputAsset, outputAsset;
            IJob objIJob;

            // Declare a new job.
            objIJob = objCloudMediaContext.Jobs.Create(jobName);

            //add tasks to the job (using preset string or preset file for WAME)
            inputAsset = objIAssetIn;
            AddTaskToJob(objIJob, "Windows Azure Media Encoder",  "H264 Encoding Task", inputAsset, configFilePath + @"\EncoderPreset_MBR_MP4_NBCSG_7Bitrates.xml", "NBCSG_7Bitrates Encoded Asset", TaskOptions.None, AssetCreationOptions.None, out outputAsset);
            inputAsset = outputAsset;
            AddTaskToJob(objIJob, "Windows Azure Media Packager", "MP4 to Smooth Task", inputAsset, configFilePath + @"\MediaPackager_MP4ToSmooth.xml", "Smooth Streaming Output Asset", TaskOptions.None, AssetCreationOptions.None, out outputAsset);
            inputAsset = outputAsset;
            AddTaskToJob(objIJob, "Windows Azure Media Packager", "Smooth to HLS Task", inputAsset, configFilePath + @"\MediaPackager_SmoothToHLS.xml", "HLS Output Asset",              TaskOptions.None, AssetCreationOptions.None, out outputAsset);

            // Use the following event handler to check job progress. 
            objIJob.StateChanged += new EventHandler<JobStateChangedEventArgs>(StateChanged);
            // Launch the job.
            objIJob.Submit();
            Console.WriteLine("Job Id: {0} has been submitted", objIJob.Id);

            // Optionally log job details. This displays basic job details to the console and saves them to a JobDetails-{JobId}.txt file in your output folder.
            LogJobStop(objIJob.Id);

            // Check job execution and wait for job to finish. 
            Task progressJobTask = objIJob.GetExecutionProgressTask(CancellationToken.None);
            progressJobTask.Wait();

            //output final output asset
            objIAssetOut = outputAsset;

            // If job state is Error, the event handling method for job progress should log errors.  Here we check for error state and exit if needed.
            if (objIJob.State == JobState.Error)
            {
                Console.WriteLine("\nExiting method due to job error.");
                return objIJob;
            }

            IAsset usableAsset;  //the outputAsset fails with exception: function call to member IAsset.Id is not supported
            //publish (create ILocator)
            if (publish)
            {
                usableAsset = GetAssetFromJobAndAssetName(objIJob.Id, assetToPublish);
                GetStreamingOriginLocator(usableAsset.Id, MediaContentType.SmoothStreaming, true);
            }

            //download an output asset
            if (download)
            {
                usableAsset = GetAssetFromJobAndAssetName(objIJob.Id, assetToDownload);
                DownloadAsset(usableAsset.Id, outputFolder);
            }

            //delete intermediate assets and ingest manifest
            Console.WriteLine("Hit any key to continue - give a chance to inspect the assets in portal before cleaning up");
            Console.ReadKey();

            //delete intermediate task output asset
            if (deleteIntermediateAssets)
            {
                for (int i = 0; i < objIJob.Tasks.Count - 1; i++)
                {
                    objIJob.Tasks[i].OutputAssets[0].Delete();
                }
            }

            Console.WriteLine("IAsset.Id = {0}", objIJob.Tasks[0].OutputAssets[0].Id);
            return objIJob;
        }

        static IJob DoKayakWorkflow(IAsset objIAssetIn, string outputFolder, out IAsset objIAssetOut)
        {
            //parameters to customize
            string jobName = "Workflow job";
            string configFilePath  = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\..\..";
            bool publish           = false;                                //whether to publish (the final) output asset
            string assetToPublish  = "Kayak Output Asset";                 //"PlayReady Protection Output Asset" "Smooth Streaming Output Asset"
            bool download          = false;                                //whether to download an IAsset
            string assetToDownload = "Kayak Output Asset";                 //"Thumbnails Asset" "Smooth Streaming Output Asset" "HLS Output Asset"
            bool deleteIntermediateAssets = false;                          //whether to delete intermediate assets

            //variables
            IAsset inputAsset, outputAsset;
            IJob objIJob;

            // Declare a new job.
            objIJob = objCloudMediaContext.Jobs.Create(jobName);

            //add tasks to the job (using preset string or preset file for WAME)
            inputAsset = objIAssetIn;
            AddTaskToJob(objIJob, "Digital Rapids - Kayak Cloud Engine", "Kayak Encode Task", inputAsset, "216.191.138.100,15000,,.mp4", "Kayak Output Asset", TaskOptions.None, AssetCreationOptions.None, out outputAsset);

            // Use the following event handler to check job progress. 
            objIJob.StateChanged += new EventHandler<JobStateChangedEventArgs>(StateChanged);
            DateTime start = DateTime.Now;
            // Launch the job.
            objIJob.Submit();
            Console.WriteLine("Job: {0} submitted ...", objIJob.Id);

            // Optionally log job details. This displays basic job details to the console and saves them to a JobDetails-{JobId}.txt file in your output folder.
            LogJobStop(objIJob.Id);

            // Check job execution and wait for job to finish. 
            Task progressJobTask = objIJob.GetExecutionProgressTask(CancellationToken.None);
            progressJobTask.Wait();

            //output final output asset
            objIAssetOut = outputAsset;
            Console.WriteLine("******************* Input IAsset.Id = {0}", objIAssetIn.Id);
            Console.WriteLine("Time taken for the job including queue time: {0} min", (((TimeSpan)(DateTime.Now - start)).TotalSeconds/60.0).ToString());
            Console.WriteLine("ITask.PerfMessage = {0}", objIJob.Tasks[0].PerfMessage);

            // If job state is Error, the event handling method for job progress should log errors.  Here we check for error state and exit if needed.
            if (objIJob.State == JobState.Error)
            {
                Console.WriteLine("\nExiting method due to job error.");
                return objIJob;
            }

            IAsset usableAsset;  //the outputAsset fails with exception: function call to member IAsset.Id is not supported
            //publish (create ILocator)
            if (publish)
            {
                usableAsset = GetAssetFromJobAndAssetName(objIJob.Id, assetToPublish);
                GetStreamingOriginLocator(usableAsset.Id, MediaContentType.SmoothStreaming, true);
            }

            //download an output asset
            if (download)
            {
                usableAsset = GetAssetFromJobAndAssetName(objIJob.Id, assetToDownload);
                DownloadAsset(usableAsset.Id, outputFolder);
            }

            //delete intermediate assets and ingest manifest
            //Console.WriteLine("Hit any key to continue - give a chance to inspect the assets in portal before cleaning up");
            //Console.ReadKey();

            //delete intermediate task output asset
            if (deleteIntermediateAssets)
            {
                for (int i = 0; i < objIJob.Tasks.Count - 1; i++)
                {
                    objIJob.Tasks[i].OutputAssets[0].Delete();
                }
            }

            return objIJob;
        }

        static IJob IndexerWorkflow(IAsset objIAssetIn, string outputFolder, out IAsset objIAssetOut)
        {
            //parameters to customize
            string jobName = "Media Indexer Workflow job";
            string configFilePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\..\..";
            bool download = true;                                 //whether to download an IAsset
            string assetToDownload = "Indexer Output Asset";

            //variables
            IAsset inputAsset, outputAsset;
            IJob objIJob;

            // Declare a new job.
            objIJob = objCloudMediaContext.Jobs.Create(jobName);

            //add tasks to the job (using preset string or preset file for WAME)
            inputAsset = objIAssetIn;
            AddTaskToJob(objIJob, "Azure Media Indexer", "Indexing Task", inputAsset, configFilePath + @"\MAVISConfig.xml", "Indexer Output Asset", TaskOptions.None, AssetCreationOptions.None, out outputAsset);

            // Use the following event handler to check job progress. 
            objIJob.StateChanged += new EventHandler<JobStateChangedEventArgs>(StateChanged);
            // Launch the job.
            objIJob.Submit();

            Console.WriteLine("IJob.Id = {0}", objIJob.Id);
            MonitorTaskProgress(objIJob, 20);

            // Optionally log job details. This displays basic job details to the console and saves them to a JobDetails-{JobId}.txt file in your output folder.
            LogJobStop(objIJob.Id);

            // Check job execution and wait for job to finish. 
            Task progressJobTask = objIJob.GetExecutionProgressTask(CancellationToken.None);
            progressJobTask.Wait();

            //output final output asset
            objIAssetOut = outputAsset;

            // If job state is Error, the event handling method for job progress should log errors.  Here we check for error state and exit if needed.
            if (objIJob.State == JobState.Error)
            {
                Console.WriteLine("\nExiting method due to job error.");
                return objIJob;
            }

            IAsset usableAsset;  //the outputAsset fails with exception: function call to member IAsset.Id is not supported
            //download an output asset
            if (download)
            {
                usableAsset = GetAssetFromJobAndAssetName(objIJob.Id, assetToDownload);
                DownloadAsset(usableAsset.Id, outputFolder);
            }
            
            Console.WriteLine("IAsset.Id = {0}", objIJob.Tasks[0].OutputAssets[0].Id);
            return objIJob;

        }

        static IJob StitchVideoClips(IAsset[] objIAssetsIn, string outputFolder, out IAsset objIAssetOut)
        {
            //parameters to customize
            string jobName = "Stitch Video Clips Job";
            string configFilePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\..\..";
            bool download = true;                                 //whether to download an IAsset
            string assetToDownload = "Stitch Video Clips Output Asset";

            //variables
            IAsset outputAsset;
            IJob objIJob;

            // Declare a new job.
            objIJob = objCloudMediaContext.Jobs.Create(jobName);

            //add tasks to the job (using preset string or preset file for WAME)
            //AddTaskToJob(objIJob, "Azure Media Encoder", "Stitch Video Clips Task", objIAssetsIn, configFilePath + @"\StitchAudioClipsPreset.xml", "Stitch Video Clips Output Asset", TaskOptions.None, AssetCreationOptions.None, out outputAsset);
            AddTaskToJob(objIJob, "Azure Media Encoder", "Stitch Video Clips Task", objIAssetsIn, configFilePath + @"\StitchVideoClipsPreset.xml", "Stitch Video Clips Output Asset", TaskOptions.None, AssetCreationOptions.None, out outputAsset);

            // Use the following event handler to check job progress. 
            objIJob.StateChanged += new EventHandler<JobStateChangedEventArgs>(StateChanged);
            // Launch the job.
            objIJob.Submit();

            // Optionally log job details. This displays basic job details to the console and saves them to a JobDetails-{JobId}.txt file in your output folder.
            LogJobStop(objIJob.Id);

            // Check job execution and wait for job to finish. 
            Task progressJobTask = objIJob.GetExecutionProgressTask(CancellationToken.None);
            progressJobTask.Wait();

            //output final output asset
            objIAssetOut = outputAsset;

            // If job state is Error, the event handling method for job progress should log errors.  Here we check for error state and exit if needed.
            if (objIJob.State == JobState.Error)
            {
                Console.WriteLine("\nExiting method due to job error.");
                return objIJob;
            }

            IAsset usableAsset;  //the outputAsset fails with exception: function call to member IAsset.Id is not supported
            //download an output asset
            if (download)
            {
                usableAsset = GetAssetFromJobAndAssetName(objIJob.Id, assetToDownload);
                DownloadAsset(usableAsset.Id, outputFolder);
            }

            Console.WriteLine("IAsset.Id = {0}", objIJob.Tasks[0].OutputAssets[0].Id);
            return objIJob;

        }

        #endregion

        #region REST API

        public static void TestRestApi()
        {
            string accountName = System.Configuration.ConfigurationManager.AppSettings["MediaServiceAccountName"];
            string accountKey  = System.Configuration.ConfigurationManager.AppSettings["MediaServiceAccountKey"];
            string acsToken    = RestApiLib.CRestApi.GetUrlEncodedAcsBearerToken(accountName, accountKey);
            Console.WriteLine(string.Format("ACS token = {0}", System.Web.HttpUtility.UrlDecode(acsToken)));

            string locatorPath = CreateOriginLocatorWithRest(objCloudMediaContext, accountName, accountKey, "nb:lid:UUID:9243aca8-2963-435f-92bd-346a09dac988", "nb:cid:UUID:c694eb7d-ec7d-45a8-9a4f-4f1b181ac7a5");
            Console.WriteLine(String.Format("ILocator.Path = {0}", locatorPath));
        }

        public static string CreateOriginLocatorWithRest(CloudMediaContext context, string mediaServicesAccountNameTarget,
                                                         string mediaServicesAccountKeyTarget, string locatorIdToReplicate, string targetAssetId)
        {
            //Make sure we are not asking for a duplicate:
            var locator = context.Locators.Where(l => l.Id == locatorIdToReplicate).FirstOrDefault();
            if (locator != null) return "ILocator.Id has been taken in the same WAMS";

            string locatorNewPath = "";
            string apiServer = "";

            string acsToken = RestApiLib.CRestApi.GetUrlEncodedAcsBearerToken(mediaServicesAccountNameTarget, mediaServicesAccountKeyTarget/*, scope, acsBaseAddress*/);

            if (!string.IsNullOrEmpty(acsToken))
            {
                var asset = context.Assets.Where(a => a.Id == targetAssetId).FirstOrDefault();

                var accessPolicy = context.AccessPolicies.Create("RestTest", TimeSpan.FromDays(100), AccessPermissions.Read | AccessPermissions.List);
                if (asset != null)
                {
                    string redirectedServiceUri = null;

                    var xmlResponse = RestApiLib.CRestApi.CreateLocator(apiServer, out redirectedServiceUri, acsToken,
                                                                asset.Id, accessPolicy.Id,
                                                                (int)LocatorType.OnDemandOrigin,
                                                                DateTime.UtcNow.AddMinutes(-10), locatorIdToReplicate);

                    Console.WriteLine("Redirected Service URI: " + redirectedServiceUri);
                    if (xmlResponse != null)
                    {
                        locatorNewPath = xmlResponse.GetElementsByTagName("Path")[0].InnerText;
                        Console.WriteLine(String.Format("ILocator.Id = {0}",   xmlResponse.GetElementsByTagName("Id")[0].InnerText));
                    }
                }
            }

            return locatorNewPath;
        }

        #endregion

        #region REST API for Changing Encoder RU

        public static void UpdateEncodingReservedUnits(X509Certificate2 mgmtCert, int value)
        {
            string subscriptionId = System.Configuration.ConfigurationManager.AppSettings["SubscriptionId"];
            string accountName    = System.Configuration.ConfigurationManager.AppSettings["MediaServiceAccountName"];
            string url            = string.Format("https://management.core.windows.net/{0}/services/mediaservices/Accounts/{1}/ServiceQuotas", subscriptionId, accountName);

            //construct request JSON string
            string jsonString = "";
            List<ServiceQuotaUpdateRequest> objList_ServiceQuotaUpdateRequest = new List<ServiceQuotaUpdateRequest>();
            objList_ServiceQuotaUpdateRequest.Add(new ServiceQuotaUpdateRequest
                                                                                {
                                                                                    ServiceType = "Encoding",
                                                                                    RequestedUnits = value
                                                                                });

            using (MemoryStream ms = new MemoryStream())
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<ServiceQuotaUpdateRequest>));
                serializer.WriteObject(ms, objList_ServiceQuotaUpdateRequest);
                jsonString = Encoding.Default.GetString(ms.ToArray());
            }
            
            //Request
            var request = (HttpWebRequest)WebRequest.Create(url);

            request.Method      = "PUT";
            request.Accept      = "application/json";
            request.ContentType = "application/json; charset=utf-8";
            request.Headers.Add("x-ms-version", "2011-10-01");
            request.Headers.Add("Accept-Encoding: gzip, deflate");
            request.ClientCertificates.Add(mgmtCert);

            using (Stream requestStream = request.GetRequestStream())
            {
                var requestBytes = System.Text.Encoding.ASCII.GetBytes(jsonString);
                requestStream.Write(requestBytes, 0, requestBytes.Length);
                requestStream.Close();
            }
 
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                Stream stream1 = response.GetResponseStream();
                StreamReader sr = new StreamReader(stream1);
                Console.WriteLine(sr.ReadToEnd());
            }
        }

        /// <summary>
        /// Gets the certificate matching the thumbprint from the local store.
        /// Throws an ArgumentException if a matching certificate is not found.
        /// </summary>
        /// <param name="thumbprint">The thumbprint of the certificate to find.</param>
        /// <returns>The certificate with the specified thumbprint.</returns>
        private static X509Certificate2 GetStoreCertificate(string thumbprint)
        {
            List<StoreLocation> locations = new List<StoreLocation> 
                                                    { 
                                                        StoreLocation.CurrentUser, 
                                                        StoreLocation.LocalMachine 
                                                    };

            foreach (var location in locations)
            {
                X509Store objX509Store = new X509Store("My", location);
                try
                {
                    objX509Store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                    X509Certificate2Collection objX509Certificate2Collection = objX509Store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
                    if (objX509Certificate2Collection.Count == 1)
                    {
                        return objX509Certificate2Collection[0];
                    }
                }
                finally
                {
                    objX509Store.Close();
                }
            }

            throw new ArgumentException(string.Format("A Certificate with Thumbprint '{0}' could not be located.", thumbprint));
        }

        #endregion

        #region Copy Asset

        //containerName must be lower case
        static CloudBlobContainer GetInternalCloudBlobContainer(string containerName, out CloudBlobClient objCloudBlobClient)
        {
            string storageAccountName = System.Configuration.ConfigurationManager.AppSettings["WamsStorageAccountName"];
            string storageAccountKey = System.Configuration.ConfigurationManager.AppSettings["WamsStorageAccountKey"];

            // Create a blob container in a storage account associated with Media Services account. 
            StorageCredentials objStorageCredentials = new StorageCredentials(storageAccountName, storageAccountKey);
            CloudStorageAccount objCloudStorageAccount = new CloudStorageAccount(objStorageCredentials, true);
            objCloudBlobClient = objCloudStorageAccount.CreateCloudBlobClient();
            CloudBlobContainer objCloudBlobContainer = objCloudBlobClient.GetContainerReference(containerName);

            //objCloudBlobContainer.CreateIfNotExist();

            Console.WriteLine("Internal CloudBlobContainer created.");

            return objCloudBlobContainer;
        }

        static CloudBlobContainer GetExternalCloudBlobContainer(string containerName, out CloudBlobClient objCloudBlobClient)
        {
            string storageAccountName = System.Configuration.ConfigurationManager.AppSettings["WestUSStorageAccountName"];
            string storageAccountKey = System.Configuration.ConfigurationManager.AppSettings["WestUSStorageAccountKey"];

            // Create a blob container in a storage account associated with Media Services account. 
            StorageCredentials objStorageCredentialsAccountAndKey = new StorageCredentials(storageAccountName, storageAccountKey);
            CloudStorageAccount objCloudStorageAccount = new CloudStorageAccount(objStorageCredentialsAccountAndKey, true);
            objCloudBlobClient = objCloudStorageAccount.CreateCloudBlobClient();
            CloudBlobContainer objCloudBlobContainer = objCloudBlobClient.GetContainerReference(objCloudBlobClient.BaseUri + containerName);

            //objCloudBlobContainer.CreateIfNotExist();

            Console.WriteLine("External CloudBlobContainer created.");

            return objCloudBlobContainer;
        }

        static void UploadFilesToCloudBlobContainer(CloudBlobContainer objCloudBlobContainer, string folder)
        {
            DirectoryInfo objDirectoryInfo = new DirectoryInfo(folder);
            foreach (var file in objDirectoryInfo.EnumerateFiles())
            {
                CloudBlockBlob objCloudBlockBlob = objCloudBlobContainer.GetBlockBlobReference(file.Name);

                //objCloudBlockBlob.DeleteIfExists();   //v.1.7.0 only
                //objCloudBlockBlob.UploadFile(file.FullName);
                objCloudBlockBlob.UploadFromStream(File.OpenRead(file.FullName));
                Console.WriteLine("File uploaded: {0}", file.FullName);
            }
        }

        //set .ism file in a smooth asset to be primary file
        static void SetPrimaryFile(string assetId)
        {
            IAsset objIAsset = GetAsset(assetId);

            var ismAssetFiles = objIAsset.AssetFiles.ToList().Where(f => f.Name.EndsWith(".ism", StringComparison.OrdinalIgnoreCase)).ToArray();

            if (ismAssetFiles.Count() != 1)
                throw new ArgumentException("The asset should have only one, .ism file");

            ismAssetFiles.First().IsPrimary = true;
            ismAssetFiles.First().Update();

        }

        static IAsset CopyBlobsToMediaAsset(CloudBlobContainer objCloudBlobContainer, CloudBlobClient objCloudBlobClient, string assetName)
        {
            //create destination asset.
            IAsset asset = objCloudMediaContext.Assets.Create(assetName, AssetCreationOptions.None);
            IAccessPolicy writePolicy = objCloudMediaContext.AccessPolicies.Create("writePolicy", TimeSpan.FromMinutes(120), AccessPermissions.Write);
            ILocator destinationLocator = objCloudMediaContext.Locators.CreateLocator(LocatorType.Sas, asset, writePolicy);

            // Get the asset container URI and copy blobs from mediaContainer to assetContainer.
            Uri destinationUri = new Uri(destinationLocator.Path);
            string destinationContainerName = destinationUri.LocalPath.Substring(1);
            CloudBlobContainer destinationCloudBlobContainer = objCloudBlobClient.GetContainerReference(destinationContainerName);
            IEnumerable<IListBlobItem> listBlobItems = objCloudBlobContainer.ListBlobs();

            using (var enumerator = listBlobItems.GetEnumerator())
            {
                while (enumerator.MoveNext())
                //foreach (var sourceBlob in listBlobItems)
                {
                    var sourceBlob = enumerator.Current;
                    string fileName = System.Web.HttpUtility.UrlDecode(Path.GetFileName(sourceBlob.Uri.AbsoluteUri));
                    Console.WriteLine("Copying: {0}", fileName);

                    CloudBlockBlob sourceCloudBlob = objCloudBlobContainer.GetBlockBlobReference(fileName);
                    sourceCloudBlob.FetchAttributes();
                    if (sourceCloudBlob.Properties.Length > 0)
                    {
                        IAssetFile assetFile = asset.AssetFiles.Create(fileName);
                        CloudBlockBlob destinationBlob = destinationCloudBlobContainer.GetBlockBlobReference(fileName);

                        //v 1.7.0 code. Changed
                        //destinationBlob.DeleteIfExists();
                        //destinationBlob.CopyFromBlob(sourceCloudBlob);   
                        //v 1.7.1 code.
                        destinationBlob.StartCopyFromBlob(sourceCloudBlob);

                        destinationBlob.FetchAttributes();
                        if (sourceCloudBlob.Properties.Length != destinationBlob.Properties.Length)
                            Console.WriteLine("Failed to copy");
                    }
                }
            }

            destinationLocator.Delete();
            writePolicy.Delete();

            // Refresh the asset.
            asset = objCloudMediaContext.Assets.Where(a => a.Id == asset.Id).FirstOrDefault();

            return asset;
        }

        //external: non-WAMS storage, internal: WAMS storage
        static IAsset CopyNonWAMSBlobsToMediaAsset(CloudBlobContainer objCloudBlobContainerExternal, CloudBlobClient objCloudBlobClientInternal, string assetName)
        {
            // Get the SAS token to use for all blobs if dealing with multiple accounts
            string blobToken = objCloudBlobContainerExternal.GetSharedAccessSignature(new SharedAccessBlobPolicy()
            {
                // Specify the expiration time for the signature.
                SharedAccessExpiryTime = DateTime.Now.AddMinutes(30),
                // Specify the permissions granted by the signature.
                Permissions = SharedAccessBlobPermissions.Write | SharedAccessBlobPermissions.Read
            });

            //create destination asset.
            IAsset objIAsset = objCloudMediaContext.Assets.Create(assetName, AssetCreationOptions.None);
            IAccessPolicy writePolicy = objCloudMediaContext.AccessPolicies.Create("writePolicy", TimeSpan.FromMinutes(120), AccessPermissions.Write);
            ILocator destinationLocator = objCloudMediaContext.Locators.CreateLocator(LocatorType.Sas, objIAsset, writePolicy);

            // Get the asset container URI and Blob copy from mediaContainer to assetContainer.
            string destinationContainerName = (new Uri(destinationLocator.Path)).Segments[1];

            CloudBlobContainer objCloudBlobContainerInternal = objCloudBlobClientInternal.GetContainerReference(destinationContainerName);

            foreach (var sourceBlob in objCloudBlobContainerExternal.ListBlobs())
            {
                string fileName = System.Web.HttpUtility.UrlDecode(Path.GetFileName(sourceBlob.Uri.AbsoluteUri));
                CloudBlockBlob objCloudBlockBlobSource = objCloudBlobContainerExternal.GetBlockBlobReference(fileName);
                objCloudBlockBlobSource.FetchAttributes();
                Console.WriteLine("Copying: {0}", fileName);

                if (objCloudBlockBlobSource.Properties.Length > 0)
                {
                    //assetContainer.CreateIfNotExist();  //do not use it in v 1.7.1
                    CloudBlockBlob objCloudBlobDestination = objCloudBlobContainerInternal.GetBlockBlobReference(fileName);

                    objCloudBlobDestination.StartCopyFromBlob(new Uri(sourceBlob.Uri.AbsoluteUri + blobToken));

                    while (true)
                    {
                        // The StartCopyFromBlob is an async operation, so we want to check if the copy operation is completed before proceeding. 
                        // To do that, we call FetchAttributes on the blob and check the CopyStatus. 
                        objCloudBlobDestination.FetchAttributes();
                        if (objCloudBlobDestination.CopyState.Status != CopyStatus.Pending)
                        {
                            Console.WriteLine("Copying " + sourceBlob.Uri.AbsoluteUri);
                            break;
                        }
                        //It's still not completed. So wait for some time.
                        System.Threading.Thread.Sleep(1000);
                    }

                    var assetFile = objIAsset.AssetFiles.Create(fileName);
                }
            }

            destinationLocator.Delete();
            writePolicy.Delete();

            // Refresh the asset.
            objIAsset = objCloudMediaContext.Assets.Where(a => a.Id == objIAsset.Id).FirstOrDefault();

            //At this point, you can create a job using your asset.
            Console.WriteLine("You are ready to use " + objIAsset.Name);

            return objIAsset;
        }

        //copy blob between Azure storage, has nothing to do with WAMS, therefore does not need WAMS namespaces. Uses Microsoft.WindowsAzure.Storage
        public static void CopyBetweenStoages(string srcContainerName, string srcAccountName, string srcAccountKey,
                                              string destContainerName, string destAccountName, string destAccountKey)
        {
            var srcAccount  = new Microsoft.WindowsAzure.Storage.CloudStorageAccount(new Microsoft.WindowsAzure.Storage.Auth.StorageCredentials(srcAccountName, srcAccountKey),   true);
            var destAccount = new Microsoft.WindowsAzure.Storage.CloudStorageAccount(new Microsoft.WindowsAzure.Storage.Auth.StorageCredentials(destAccountName, destAccountKey), true);

            var srcCloudBlobClient  = srcAccount.CreateCloudBlobClient();
            var destCloudBlobClient = destAccount.CreateCloudBlobClient();

            var srcCloudBlobContainer  = srcCloudBlobClient.GetContainerReference(srcContainerName);
            var destCloudBlobContainer = destCloudBlobClient.GetContainerReference(destContainerName);
            destCloudBlobContainer.CreateIfNotExists();

            string blobToken = srcCloudBlobContainer.GetSharedAccessSignature(new Microsoft.WindowsAzure.Storage.Blob.SharedAccessBlobPolicy()
            {
                // Specify the expiration time for the signature.
                SharedAccessExpiryTime = DateTime.Now.AddMinutes(300),
                // Specify the permissions granted by the signature.
                Permissions = Microsoft.WindowsAzure.Storage.Blob.SharedAccessBlobPermissions.Write | Microsoft.WindowsAzure.Storage.Blob.SharedAccessBlobPermissions.Read
            });

            foreach (var sourceBlob in srcCloudBlobContainer.ListBlobs())
            {
                string fileName = System.Web.HttpUtility.UrlDecode(Path.GetFileName(sourceBlob.Uri.AbsoluteUri));
                var srcCloudBlockBlob = srcCloudBlobContainer.GetBlockBlobReference(fileName);
                srcCloudBlockBlob.FetchAttributes();

                if (srcCloudBlockBlob.Properties.Length > 0)
                {
                    var destCloudBlockBlob = destCloudBlobContainer.GetBlockBlobReference(fileName);
                    destCloudBlockBlob.StartCopyFromBlob(new Uri(sourceBlob.Uri.AbsoluteUri + blobToken));

                    while (true)
                    {
                        // The StartCopyFromBlob is an async operation, so we want to check if the copy operation is completed before proceeding. 
                        // To do that, we call FetchAttributes on the blob and check the CopyStatus. 
                        destCloudBlockBlob.FetchAttributes();
                        if (destCloudBlockBlob.CopyState.Status != Microsoft.WindowsAzure.Storage.Blob.CopyStatus.Pending)
                        {
                            break;
                        }
                        //It's still not completed. So wait for some time.
                        System.Threading.Thread.Sleep(1000);
                    }
                }

                Console.WriteLine(string.Format("Copying file {0}", fileName));
            }

            Console.WriteLine(string.Format("Done copying from {0} to {1}", srcAccountName, destAccountName));
        }
        #endregion

        #region Job diagnostics

        public static void GetJobInfo(string jobId, CloudMediaContext objCloudMediaContext)
        {
            string FORMAT = "{0,-60}{1,-500}";
            IJob objIJob = GetJob(jobId);

            //basic info
            TimeSpan duration = (objIJob.State == JobState.Processing)? (TimeSpan)(DateTime.UtcNow - objIJob.StartTime) : objIJob.RunningDuration;
            Console.WriteLine(FORMAT, "ID", objIJob.Id);
            Console.WriteLine(FORMAT, "Name", objIJob.Name);
            Console.WriteLine(FORMAT, "State", objIJob.State.ToString());
            Console.WriteLine(FORMAT, "RunningDuration", duration.TotalHours.ToString() + "(Hr)");
            Console.WriteLine(FORMAT, "StartTime", objIJob.StartTime.Value.ToString("MM-dd-yyyy HH:mm:ss"));

            //list all output assets
            foreach (IAsset objIAsset in objIJob.OutputMediaAssets)
            {
                Console.WriteLine(FORMAT, "IAsset.Name", objIAsset.Name);
                Console.WriteLine(FORMAT, "objIAsset.AssetFiles.Count<IAssetFile>()", objIAsset.AssetFiles.Count<IAssetFile>().ToString());
            }

            //list all ITask
            foreach (ITask objITask in objIJob.Tasks)
            {
                Console.WriteLine(FORMAT, "Task ID (Name, Progress)", string.Format("{0} ({1}, {2})", objITask.Id, objITask.Name, objITask.Progress.ToString() + "%"));
            }

            //Exception info
            Task objTask = objIJob.GetExecutionProgressTask(System.Threading.CancellationToken.None);
            if (objTask.Exception != null)
            {
                Console.WriteLine(FORMAT, "System.Threading.Task.Exception", objTask.Exception.ToString());
            }

            if (objIJob.State == JobState.Error)
            {
                foreach (ITask objITask in objIJob.Tasks)
                {
                    foreach (ErrorDetail objErrorDetail in objITask.ErrorDetails)
                    {
                        Console.WriteLine(FORMAT, "ITask.Id", objITask.Id);
                        Console.WriteLine(FORMAT, "ITask.Name", objITask.Name);
                        Console.WriteLine(FORMAT, "ErrorDetails.Code", objErrorDetail.Code);
                        Console.WriteLine(FORMAT, "ErrorDetail.Message", objErrorDetail.Message);
                    }
                }
            }

        }
        #endregion


    }  //class

    public class ServiceQuotaUpdateRequest
    {
        public string ServiceType { get; set; }
        public int RequestedUnits { get; set; }
    }
}  //namespace
