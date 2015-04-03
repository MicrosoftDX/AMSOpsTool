using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.MediaServices.Client.DynamicEncryption;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using CreateManifestFilter;
using SmoothStreamingManifestGenerator;

namespace PrototypeLive
{
    public class WamsOperations
    {
        static string[] CHANNEL_NAMES = new string[] { 
                                                      "chaventus"
                                                      //"nbc01", "nbc02", "nbc03", "nbc04", "nbc05", "nbc06", "nbc07", "nbc08", "nbc09", "nbc10", "nbc11", "nbc12",  "nbc13", "nbc14", "nbc15"                                           
                                                      //"nbc16", "nbc17", "nbc18", "nbc19", "nbc20", 
                                                      //"nbc24", 
                                                      //"mds01", "mds02", "mds03", "mds04", "mds05", "mds06", 
                                                      //"dx01", "dx02", "dx03", "dx04", "dx05", "dx06", "dx07", "dx08", "dx09", "dx10", 
                                                      //"dx11", "dx12", 
                                                      //"dx13", "dx14", "dx15", "dx16", "dx17", 
                                                      //"dx18", "dx19", "dx20"//, 
                                                      //,"vod", "default"
                                                     };
        static System.IO.StreamWriter objStreamWriter = null;   //for write content to a text file
        static long totalDelay = 0;                             //to get a total archive delay across all fragblobs of all quality levels

        #region Media service level operations

        public static void MediaServiceSnapshot(CloudMediaContext objCloudMediaContext)
        {
            foreach (var storage in objCloudMediaContext.StorageAccounts)
            {
                Console.WriteLine("IStorageAccount.Name: {0}, IStorageAccount.IsDefault = {1}", storage.Name, storage.IsDefault.ToString());
            }

            string FORMAT_STRING = "{0,-30} {1,-10} {2}";
            //channels
            ColorConsole.WriteLine(string.Format(FORMAT_STRING, "Channel", "State", "ServiceID/ChannelID"), ConsoleColor.BackgroundBlue);
            var sortedChannels = objCloudMediaContext.Channels.AsEnumerable().OrderBy(ch => ch.Name);

            foreach (IChannel channel in sortedChannels)
            {
                if (channel.State == ChannelState.Running)
                {
                    ColorConsole.WriteLine(string.Format(FORMAT_STRING, channel.Name, channel.State.ToString(), channel.Id), ConsoleColor.ForegroundRed | ConsoleColor.BackgroundCyan);
                }
                else
                {
                    Console.WriteLine(FORMAT_STRING, channel.Name, channel.State.ToString(), channel.Id);
                }
                foreach (IProgram prog in channel.Programs)
                {
                    if (prog.State == ProgramState.Running)
                    {
                        ColorConsole.WriteLine(string.Format("\t\t" + FORMAT_STRING, prog.Name, prog.State.ToString(), prog.Id), ConsoleColor.ForegroundGreen | ConsoleColor.BackgroundCyan);
                    }
                    else
                    {
                        Console.WriteLine("\t\t" + FORMAT_STRING, prog.Name, prog.State.ToString(), prog.Id);
                    }
                }
            }

            //origins
            ColorConsole.WriteLine(string.Format(FORMAT_STRING, "Origin", "State", "ServiceID/OriginID"), ConsoleColor.BackgroundBlue);
            var sortedOrigins = objCloudMediaContext.StreamingEndpoints.AsEnumerable().OrderBy(origin => origin.Name);
            foreach (IStreamingEndpoint origin in sortedOrigins)
            {
                if (origin.State == StreamingEndpointState.Running)
                {
                    ColorConsole.WriteLine(string.Format(FORMAT_STRING, origin.Name, origin.State.ToString(), origin.Id), ConsoleColor.ForegroundRed | ConsoleColor.BackgroundCyan);
                }
                else
                {
                    Console.WriteLine(FORMAT_STRING, origin.Name, origin.State.ToString(), origin.Id.Substring(12));
                }
            }
        }

        public static void MapMediaServices()
        {
            string ingestUrlE, ingestUrlW, previewUrlE, previewUrlW;
            bool originEastProtected = false;
            bool originWestProtected = false;
            string eastChannelServiceId, eastOriginServiceId, westChannelServiceId, westOriginServiceId;
            string eastChannelMdsEvent, eastOriginMdsEvent, westChannelMdsEvent, westOriginMdsEvent;

            //constants
            string TR_FORMAT        = "<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}</td><td>{5}</td><td>{6}</td><td>{7}</td><td>{8}</td><td>{9}</td><td>{10}</td><td>{11}</td><td>{12}</td><td>{13}</td><td>{14}</td><td>{15}</td><td>{16}</td><td>{17}</td><td>{18}</td><td>{19}</td></tr>";
            string TH_FORMAT        = "<tr><th>{0}</th><th>{1}</th><th>{2}</th><th>{3}</th><th>{4}</th><th>{5}</th><th>{6}</th><th>{7}</th><th>{8}</th><th>{9}</th><th>{10}</th><th>{11}</th><th>{12}</th><th>{13}</th><th>{14}</th><th>{15}</th><th>{16}</th><th>{17}</th><th>{18}</th><th>{19}</th></tr>";
            string INGEST_FORMAT    = "http://{0}.nbcsgliveprod{1}ms.channel.mediaservices.windows.net/ingest.isml";
            string PREVIEW_FORMAT   = "http://{0}.nbcsgliveprod{1}ms.channel.mediaservices.windows.net/preview.isml/manifest";
            string INGEST_FORMAT1   = "<a href=\"{0}\">Ingest</a>";
            string PREVIEW_FORMAT1  = "<a href=\"http://smf.cloudapp.net/healthmonitor?Url={0}\" target=\"_blank\">Preview</a>";
            string MDS_QUERY_FORMAT = "<a href=\"https://production.diagnostics.monitoring.core.windows.net/content/search/search.html?table={0}&offset=-00%3a30%3a00&query=%22ServiceId%3d%22%22{1}%22%22%22\" target=\"_blank\">{2}</a>";
            string MDS_EVENT_QUERY  = "<a href=\"https://production.diagnostics.monitoring.core.windows.net/content/search/search.html?table={0}&offset=-00%3a30%3a00&&query=%22DeploymentId%3d%22%22{1}%22%22+and+level+%3c+4%22\" target=\"_blank\">{2}</a>";   //MDS Event Log table

            //CloudMediaContext
            AcsInstance objAcsInstance  = AcsInstance.Preferred;
            string mediaServiceNameEast = "nbcsgliveprodeastms";
            string mediaServiceNameWest = "nbcsgliveprodwestms";
            string mediaServiceKeyEast  = System.Configuration.ConfigurationManager.AppSettings[mediaServiceNameEast];
            string mediaServiceKeyWest  = System.Configuration.ConfigurationManager.AppSettings[mediaServiceNameWest];
            AcsHelper objAcsHelperEast  = new AcsHelper(objAcsInstance, mediaServiceNameEast, mediaServiceKeyEast);
            AcsHelper objAcsHelperWest  = new AcsHelper(objAcsInstance, mediaServiceNameWest, mediaServiceKeyWest);
            CloudMediaContext objCloudMediaContextEast = objAcsHelperEast.GetCloudMediaContext();
            CloudMediaContext objCloudMediaContextWest = objAcsHelperWest.GetCloudMediaContext();

            //IChannel
            var sortedChannelsEast = objCloudMediaContextEast.Channels.AsEnumerable().OrderBy(ch => ch.Name);
            var sortedChannelsWest = objCloudMediaContextWest.Channels.AsEnumerable().OrderBy(ch => ch.Name);
            Dictionary<string, IChannel> objDictionaryWest = new Dictionary<string, IChannel>();
            foreach (IChannel channelWest in sortedChannelsWest)
            {
                objDictionaryWest.Add(channelWest.Name, channelWest);
            }
            IChannel objIChannelWest;

            //IOrigin
            Dictionary<string, IStreamingEndpoint> objDictionaryOE = new Dictionary<string, IStreamingEndpoint>();
            Dictionary<string, IStreamingEndpoint> objDictionaryOW = new Dictionary<string, IStreamingEndpoint>();

            foreach (IStreamingEndpoint objIOrigin in objCloudMediaContextEast.StreamingEndpoints)
            {
                objDictionaryOE.Add(objIOrigin.Name, objIOrigin);
            }
            foreach (IStreamingEndpoint objIOrigin in objCloudMediaContextWest.StreamingEndpoints)
            {
                objDictionaryOW.Add(objIOrigin.Name, objIOrigin);
            }
            IStreamingEndpoint objIOriginEast, objIOriginWest;

            Console.WriteLine("Starting iterating through the channels");

            Console.WriteLine(TH_FORMAT,
                              "Channel (E)", "Channel Sink MDS Log (E)", "Channel Sink MDS Events (E)", "Ingest (E)", "Preview (E)", "Origin (E)", "RU (E)", "Origin MDS Log (E)", "Origin MDS Events (E)", "Origin Protected (E)",
                              "Channel (W)", "Channel Sink MDS Log (W)", "Channel Sink MDS Events (W)", "Ingest (W)", "Preview (W)", "Origin (W)", "RU (W)", "Origin MDS Log (W)", "Origin MDS Events (W)", "Origin Protected (W)");

            foreach (IChannel objIChannelEast in sortedChannelsEast)
            {
                objIChannelWest = objDictionaryWest[objIChannelEast.Name];
                objIOriginEast  = objDictionaryOE[objIChannelEast.Name];
                objIOriginWest  = objDictionaryOW[objIChannelEast.Name];
                ingestUrlE      = string.Format(INGEST_FORMAT,  objIChannelEast.Name, "east");
                ingestUrlW      = string.Format(INGEST_FORMAT,  objIChannelWest.Name, "west");
                previewUrlE     = string.Format(PREVIEW_FORMAT, objIChannelEast.Name, "east");
                previewUrlW     = string.Format(PREVIEW_FORMAT, objIChannelWest.Name, "west");
                ingestUrlE      = string.Format(INGEST_FORMAT1, ingestUrlE);
                ingestUrlW      = string.Format(INGEST_FORMAT1, ingestUrlW);
                previewUrlE     = string.Format(PREVIEW_FORMAT1, previewUrlE);
                previewUrlW     = string.Format(PREVIEW_FORMAT1, previewUrlW);
                
                //Service ID: link to MDS Log Table
                eastChannelServiceId = string.Format(MDS_QUERY_FORMAT, "WAMSStreamingBL2SvcCentralLogsTableVer1v0", objIChannelEast.Id.Substring(13), "MDS Log");
                eastOriginServiceId  = string.Format(MDS_QUERY_FORMAT, "WAMSStreamingBL2SvcCentralLogsTableVer1v0", objIOriginEast.Id.Substring(12),  "MDS Log");
                westChannelServiceId = string.Format(MDS_QUERY_FORMAT, "WAMSStreamingBY1SvcCentralLogsTableVer1v0", objIChannelWest.Id.Substring(13), "MDS Log");
                westOriginServiceId  = string.Format(MDS_QUERY_FORMAT, "WAMSStreamingBY1SvcCentralLogsTableVer1v0", objIOriginWest.Id.Substring(12),  "MDS Log");
                
                //Deployment ID: link to MDS Event Log Table
                eastChannelMdsEvent = string.Format(MDS_EVENT_QUERY, "WAMSStreamingBL2SvcCentralEventLogsTableVer1v0", "DeploymentID from ServiceID link", "MDS EventLog");
                eastOriginMdsEvent  = string.Format(MDS_EVENT_QUERY, "WAMSStreamingBL2SvcCentralEventLogsTableVer1v0", "DeploymentID from ServiceID link", "MDS EventLog");
                westChannelMdsEvent = string.Format(MDS_EVENT_QUERY, "WAMSStreamingBY1SvcCentralEventLogsTableVer1v0", "DeploymentID from ServiceID link", "MDS EventLog");
                westOriginMdsEvent  = string.Format(MDS_EVENT_QUERY, "WAMSStreamingBY1SvcCentralEventLogsTableVer1v0", "DeploymentID from ServiceID link", "MDS EventLog");

                //origin AllowedIPv4List
                //if (objIOriginEast.AccessControl != null && objIOriginEast.AccessControl.Playback != null && objIOriginEast.Settings.Playback.Security != null && objIOriginEast.Settings.Playback.Security.IPv4AllowList.Count > 0 && objIOriginEast.Settings.Playback.Security.IPv4AllowList[0].IP != "0.0.0.0/0")
                //    originEastProtected = true;
                //else
                //    originEastProtected = false;
                //if (objIOriginWest.Settings != null && objIOriginWest.Settings.Playback != null && objIOriginWest.Settings.Playback.Security != null && objIOriginWest.Settings.Playback.Security.IPv4AllowList.Count > 0 && objIOriginWest.Settings.Playback.Security.IPv4AllowList[0].IP != "0.0.0.0/0")
                //    originWestProtected = true;
                //else
                //    originWestProtected = false;

                Console.WriteLine(TR_FORMAT,
                                  objIChannelEast.Name, eastChannelServiceId, eastChannelMdsEvent, ingestUrlE, previewUrlE, objIOriginEast.Name, objIOriginEast.ScaleUnits.Value.ToString(), eastOriginServiceId, eastOriginMdsEvent, originEastProtected.ToString(),
                                  objIChannelWest.Name, westChannelServiceId, westChannelMdsEvent, ingestUrlW, previewUrlW, objIOriginWest.Name, objIOriginWest.ScaleUnits.Value.ToString(), westOriginServiceId, westOriginMdsEvent, originWestProtected.ToString());
            }

        }

        //also writes to text file
        public static void ListAllAssets(CloudMediaContext objCloudMediaContext)
        {
            DateTime MIN_DATE = new DateTime(2013, 8, 1);  //contents no earlier than this date.

            //prepare file write
            string fileName = AppDomain.CurrentDomain.SetupInformation.ApplicationBase + @"NBC_Live_Archives_USWest_upto_7_29_2014_SortByDate.txt";
            System.IO.FileStream objFileStream = new System.IO.FileStream(fileName, System.IO.FileMode.Append, System.IO.FileAccess.Write);
            System.IO.StreamWriter objStreamWriter = new System.IO.StreamWriter(objFileStream);         // create a Char writer          

            string FORMAT = "{0,-60} {1,-60} {2,-60} {3,-30} {4,-60} {5}";
            string ismFileName;
            string line, url;
            ILocator objILocator;

            //objCloudMediaContext.Assets is limited to the first 1000 items
            int DAYS       = 30;
            DateTime start = DateTime.Now.AddDays(-DAYS);
            DateTime end   = DateTime.Now;

            //header
            line = string.Format(FORMAT, "IAsset.Id", "IAsset.Name", ".ism File Name", "IAsset.LastModified", "StorageAccountName", "URI");
            Console.WriteLine(line);
            objStreamWriter.BaseStream.Seek(0, System.IO.SeekOrigin.End);      // set the file pointer to the end
            objStreamWriter.WriteLine(line);
            //data
            var assets = objCloudMediaContext.Assets.Where(a => (a.LastModified <= end) && (a.LastModified > start)).ToList().OrderByDescending(a => a.LastModified);
            while (assets.Count() > 0)
            {
                Console.WriteLine("From {0} to {1}", start.ToString("MM/dd/yyyy HH:mm:ss"), end.ToString("MM/dd/yyyy HH:mm:ss"));
                foreach (IAsset objIAsset in assets)
                {
                    ismFileName = GetIsmFileName(objIAsset.Id, objCloudMediaContext);

                    //print orign URLs - very slow
                    objILocator = GetOriginLocator(objIAsset.Id, objCloudMediaContext);
                    url = GetOriginUri(objILocator);
                    line = string.Format(FORMAT, objIAsset.Id, objIAsset.Name, ismFileName, objIAsset.LastModified.ToString("MM/dd/yyyy HH:mm:ss"), objIAsset.StorageAccountName, url/*objIAsset.Uri.OriginalString*/);

                    //print SAS URLs - very fast
                    //line = string.Format(FORMAT, objIAsset.Id, objIAsset.Name, ismFileName, objIAsset.LastModified.ToString("MM/dd/yyyy HH:mm:ss"), objIAsset.StorageAccountName, objIAsset.Uri.OriginalString);
                    
                    //ListAssetFiles(objIAsset.Id, objCloudMediaContext);
                    Console.WriteLine(line);
                    //write to file
                    objStreamWriter.BaseStream.Seek(0, System.IO.SeekOrigin.End);      // set the file pointer to the end
                    objStreamWriter.WriteLine(line);
                }

                //next time window
                start = start.AddDays(-DAYS);
                end = end.AddDays(-DAYS);
                assets = objCloudMediaContext.Assets.Where(a => (a.LastModified <= end) && (a.LastModified > start) && (a.LastModified > MIN_DATE)).ToList().OrderByDescending(a => a.LastModified);
            }

            //close stream writer
            objStreamWriter.Flush();
            objStreamWriter.Close();
        }

        public static void ResetAllChannels(CloudMediaContext objCloudMediaContext)
        {
            IChannel objIChannel;
            IOperation objIOperation;
            foreach (string channelName in CHANNEL_NAMES)
            {
                objIChannel = objCloudMediaContext.Channels.Where(c => c.Name == channelName).FirstOrDefault();
                if (!HasRunningProgram(objIChannel))
                {
                    objIOperation = objIChannel.SendResetOperation();
                    Console.WriteLine("Channel {0} IChannel.SendResetOperation has been sent.", objIChannel.Name);
                }
                else
                {
                    Console.WriteLine("Channel {0} has running program. Not reset.", objIChannel.Name);
                }
            }
        }

        private static bool HasRunningProgram(IChannel objIChannel)
        {
            bool hasRunningProgram = false;
            foreach (IProgram objIProgram in objIChannel.Programs)
            {
                if (objIProgram.State == ProgramState.Running)
                {
                    hasRunningProgram = true;
                    break;
                }
            }
            return hasRunningProgram;
        }
        #endregion

        #region Asset level operations

        //what we can get from an URL, origin or edge
        public static void GetInfoFromUrl(string url, CloudMediaContext objCloudMediaContext)
        {
            //whether to write something to a text file
            if (bool.Parse(System.Configuration.ConfigurationManager.AppSettings["WriteOutputToFile"]))
            {
                objStreamWriter = GetStreamWriter(System.Configuration.ConfigurationManager.AppSettings["OutputFileName"]);
            }

            ConsoleColor objConsoleColor = ConsoleColor.ForegroundBlue | ConsoleColor.BackgroundCyan;

            //use smooth URL
            int endIndex = url.IndexOf("(format=");
            if (endIndex > 0)
            {   //if has format string
                url = url.Substring(0, endIndex);
            }

            IAsset objIAsset;
            //input can be either URL or IAsset.Id
            if (url.StartsWith("nb:cid:UUID"))
            {   //IAsset.Id
                objIAsset = WamsOperations.GetAsset(url, objCloudMediaContext);
            }
            else
            {   //URL
                objIAsset = WamsOperations.GetAssetFromOriginUrl(url, objCloudMediaContext);
            }
            if (objIAsset == null)
            {
                ColorConsole.WriteLine("Incorrect media service name/key pair is used.", ConsoleColor.ForegroundRed);
                return;
            }

            ILocator objILocatorOri = GetOriginLocator(objIAsset.Id, objCloudMediaContext);
            ILocator objILocatorSas = GetSasLocator(objIAsset.Id, objCloudMediaContext);
            string ismFileName      = WamsOperations.GetIsmFileName(objIAsset.Id, objCloudMediaContext);
            if (string.IsNullOrEmpty(ismFileName))
            {
                //empty asset, exit
                ColorConsole.WriteLine(string.Format("Asset {0} is empty.", objIAsset.Id), ConsoleColor.ForegroundRed);
                return;
            }
            string sasUrl           = WamsOperations.GetSasUriForFile(objILocatorSas, ismFileName);
            string hss              = string.Format("{0}{1}/manifest", objILocatorOri.Path, ismFileName);
            string hls              = string.Format("{0}{1}/manifest(format=m3u8-aapl)", objILocatorOri.Path, ismFileName);
            string hds              = string.Format("{0}{1}/manifest(format=f4m-f4f).f4m", objILocatorOri.Path, ismFileName);
            url                     = hss;
            IProgram objIProgram    = GetProgramFromAssetId(objIAsset.Id, objCloudMediaContext);

            if (objStreamWriter != null)
            {
                objStreamWriter.BaseStream.Seek(0, System.IO.SeekOrigin.End);     
                objStreamWriter.WriteLine(string.Format("SAS URL: {0}", sasUrl));
                objStreamWriter.BaseStream.Seek(0, System.IO.SeekOrigin.End);
                objStreamWriter.WriteLine(string.Format("HSS URL: {0}", hss));
            }

            //IAsset basics
            ColorConsole.WriteLine("Locators and Identifiers", objConsoleColor);
            string FORMAT = "{0,-30}{1,-30}";
            ColorConsole.WriteLine(string.Format(FORMAT, "Asset ID/Locator", "Value"), ConsoleColor.ForegroundGreen);
            Console.WriteLine(FORMAT, "IAsset.Id",          objIAsset.Id);
            Console.WriteLine(FORMAT, "IAsset.AlternateId", objIAsset.AlternateId);
            Console.WriteLine(FORMAT, "IAsset.Name",        objIAsset.Name);
            Console.WriteLine(FORMAT, "IAsset.Created",     objIAsset.Created.ToString("MM/dd/yyyy HH:mm:ss"));
            Console.WriteLine(FORMAT, "IAsset.LastModified",objIAsset.LastModified.ToString("MM/dd/yyyy HH:mm:ss"));
            Console.WriteLine(FORMAT, "IAsset.State",       objIAsset.State.ToString());
            Console.WriteLine(FORMAT, "IAsset.Options",     objIAsset.Options.ToString());
            Console.WriteLine(FORMAT, "StorageAccountName", objIAsset.StorageAccountName);
            Console.WriteLine(FORMAT, "Origin ILocator.Id", objILocatorOri.Id);
            Console.WriteLine(FORMAT, "SAS ILocator.Id",    objILocatorSas.Id);
            Console.WriteLine(FORMAT, "SAS URI",            sasUrl);
            Console.WriteLine(FORMAT, "HSS URL",            hss);
            Console.WriteLine(FORMAT, "HLS URL",            hls);
            Console.WriteLine(FORMAT, "HDS URL",            hds);
            Console.WriteLine(string.Empty);

            //Program and Channel for Live only
            ColorConsole.WriteLine("Program and Channel", objConsoleColor);
            WamsOperations.ListProgramChannel(objIAsset.Id, objCloudMediaContext);
            Console.WriteLine(string.Empty);

            //ManifestInfo
            ColorConsole.WriteLine("Client Manifest", objConsoleColor);
            WamsOperations.ListManifestInfo(url);
            Console.WriteLine(string.Empty);
            ColorConsole.WriteLine("Video Times", objConsoleColor);
            WamsOperations.ListVideoTimes(url);
            Console.WriteLine(string.Empty);
            
            //IAsset details
            ColorConsole.WriteLine("Asset Files", objConsoleColor);
            WamsOperations.ListAssetFiles(objIAsset.Id, objCloudMediaContext);
            Console.WriteLine(string.Empty);
            //Console.WriteLine(WamsOperations.FromOriginUriToSasUri(url, objCloudMediaContext));
            ColorConsole.WriteLine("Locators", objConsoleColor);
            WamsOperations.ListLocators(url, objCloudMediaContext);
            Console.WriteLine(string.Empty);
            ColorConsole.WriteLine("Dynamic Manifest Filters (Virtual Cut)", objConsoleColor);
            WamsOperations.ListManifestFilters(objIAsset.Id, objCloudMediaContext);
            Console.WriteLine(string.Empty);
            ColorConsole.WriteLine("Server Manifest", objConsoleColor);
            WamsOperations.ListServerManifest(sasUrl);
            Console.WriteLine(string.Empty);
            ColorConsole.WriteLine("Storage: Fragblobs or .ismv/a Files", objConsoleColor);
            WamsOperations.ListFragBlobs(objIAsset.Id, objCloudMediaContext);

            //close stream writer
            if (objStreamWriter != null)
            {
                objStreamWriter.Flush();
                objStreamWriter.Close();
            }
        }

        public static System.IO.StreamWriter GetStreamWriter(string filename)
        {
            string fileName = AppDomain.CurrentDomain.SetupInformation.ApplicationBase + filename;
            System.IO.FileStream objFileStream = new System.IO.FileStream(fileName, System.IO.FileMode.Append, System.IO.FileAccess.Write);
            System.IO.StreamWriter objStreamWriter = new System.IO.StreamWriter(objFileStream);
            objStreamWriter.BaseStream.Seek(0, System.IO.SeekOrigin.End);      // set the file pointer to the end
            return objStreamWriter;
        }

        //ad hoc asset search
        public static IAsset FindAssetByAssetFileName(string filename, CloudMediaContext objCloudMediaContext)
        {
            IAssetFile objIAssetFile = null;
            foreach (IAsset objIAsset in objCloudMediaContext.Assets)
            {
                objIAssetFile = objIAsset.AssetFiles.Where(f => f.Name == filename).FirstOrDefault();
                if (objIAssetFile == null)
                {
                    Console.WriteLine("IAsset.Id = {0}, IAsset.Name = {1}", objIAsset.Id, objIAsset.Name);
                }
                else
                {
                    ColorConsole.WriteLine(string.Format("IAsset.Id = {0}, IAsset.Name = {1}", objIAsset.Id, objIAsset.Name), ConsoleColor.ForegroundRed);
                    return objIAsset;
                }
            }
            return null;
        }

        public static IAsset FindAssetByAssetName(string assetname, CloudMediaContext objCloudMediaContext)
        {
            IAsset objIAsset = objCloudMediaContext.Assets.Where(a => a.Name.ToLower() == assetname.ToLower()).FirstOrDefault();
            if (objIAsset != null)
            {
                Console.WriteLine("IAsset.Id = {0}, IAsset.Name = {1}", objIAsset.Id, objIAsset.Name);
            }
            else
            {
                Console.WriteLine("IAsset.Name {1} not found.", objIAsset.Name);
            }
            return objIAsset;
        }

        public static IAsset FindAssetByPid(int pid, CloudMediaContext objCloudMediaContext)
        {
            IAsset objIAsset = objCloudMediaContext.Assets.Where(a => a.AlternateId.Contains(pid.ToString())).FirstOrDefault();
            if (objIAsset != null)
            {
                Console.WriteLine("IAsset.Id = {0}, IAsset.Name = {1}", objIAsset.Id, objIAsset.Name);
            }
            else
            {
                Console.WriteLine("PID {1} not found.", pid);
            }
            return objIAsset;
        }

        public static void ListAssetFiles(string assetId, CloudMediaContext objCloudMediaContext)
        {
            string FORMAT = "{0,-60}{1,-30}{2,-30}";
            ColorConsole.WriteLine(string.Format(FORMAT, "IAssetFile.Id", "IAssetFile.LastModified", "IAssetFile.Name"), ConsoleColor.ForegroundGreen);

            IAsset objIAsset = GetAsset(assetId, objCloudMediaContext);
            foreach (IAssetFile objIAssetFile in objIAsset.AssetFiles)
            {
                Console.WriteLine(FORMAT, objIAssetFile.Id, objIAssetFile.LastModified.ToString("MM/dd/yyyy HH:mm:ss"), objIAssetFile.Name);
            }
        }

        private static CloudBlobContainer GetCloudBlobContainer(IAsset objIAsset, CloudMediaContext objCloudMediaContext)
        {
            //get SAS locator of the given asset, which will provide containerName
            ILocator sasLocator = GetSasLocator(objIAsset.Id, objCloudMediaContext);
            Uri uri = new Uri(sasLocator.Path);
            string containerName = uri.LocalPath.Substring(1);

            //create CloudBlobContainer
            string accountName = objIAsset.StorageAccountName;
            string accountKey = System.Configuration.ConfigurationManager.AppSettings[accountName];
            Microsoft.WindowsAzure.Storage.Auth.StorageCredentials objStorageCredentials = new Microsoft.WindowsAzure.Storage.Auth.StorageCredentials(accountName, accountKey);

            CloudStorageAccount objCloudStorageAccount = new CloudStorageAccount(objStorageCredentials, true);
            CloudBlobClient objCloudBlobClient = objCloudStorageAccount.CreateCloudBlobClient();
            CloudBlobContainer objCloudBlobContainer = objCloudBlobClient.GetContainerReference(containerName);

            //objCloudBlobContainer.CreateIfNotExist();

            Console.WriteLine("CloudBlobContainer created/located.");

            return objCloudBlobContainer;
        }
        public static void ListFragBlobs(string assetId, CloudMediaContext objCloudMediaContext)
        {
            string FORMAT2 = "{0,-60}{1,-30}";
            string FORMAT3 = "{0,-60}{1,-30}{2,-30}{3,-30}{4,-30}{5,-30}";

            ////storage level: StorageCredentials
            //IAsset objIAsset   = GetAsset(assetId, objCloudMediaContext);
            //string accountName = objIAsset.StorageAccountName;
            //string accountKey  = System.Configuration.ConfigurationManager.AppSettings[accountName];
            //Microsoft.WindowsAzure.Storage.Auth.StorageCredentials objStorageCredentials = new Microsoft.WindowsAzure.Storage.Auth.StorageCredentials(accountName, accountKey);

            ////container level
            //ILocator sasLocator = GetSasLocator(assetId, objCloudMediaContext);
            ////really no need for StorageCredentials, SAS URI is sufficient
            //Uri uri = new Uri(sasLocator.Path);
            //CloudBlobClient objCloudBlobClient = new CloudBlobClient(new Uri(uri.Scheme + "://" + uri.Host), objStorageCredentials); //root URL
            //CloudBlobContainer objCloudBlobContainer = objCloudBlobClient.GetContainerReference(uri.LocalPath.Substring(1));  //asset container: asset-GUID, need to remove the starting "/"
            ////CloudBlobContainer objCloudBlobContainer = new CloudBlobContainer(new Uri(sasLocator.BaseUri));
            ////objCloudBlobContainer.FetchAttributes();
            
            IAsset objIAsset   = GetAsset(assetId, objCloudMediaContext);
            ILocator sasLocator = GetSasLocator(objIAsset.Id, objCloudMediaContext);
            CloudBlobContainer objCloudBlobContainer = GetCloudBlobContainer(objIAsset, objCloudMediaContext);
            IEnumerable<IListBlobItem> objIListBlobItems = objCloudBlobContainer.ListBlobs();
            Console.WriteLine("CloudBlobContainer.Uri = {0}", objCloudBlobContainer.Uri.ToString());
            Console.WriteLine("Depending on size, listing fragblob may take some time. Hit any key to continue ...");
            //Console.ReadKey();

            //IListBlobItem level
            CloudBlockBlob objCloudBlockBlob;
            CloudBlobDirectory objCloudBlobDirectory;
            CloudPageBlob objCloudPageBlob;
            IEnumerable<IListBlobItem> objIListBlobItems1;
            IListBlobItem objIListBlobItemLast, objIListBlobItemHeader;

            IList<MissingFragblob> objIList_MissingFragblob = new List<MissingFragblob>();
            
            //list all
            ColorConsole.WriteLine(string.Format(FORMAT3, "IListBlobItem", "Type", "Fragblob Count + 1 Header", "First Fragblob", "Last Fragblob", "Header"), ConsoleColor.ForegroundGreen);
            if (objIListBlobItems != null)
            {
                foreach (IListBlobItem objIListBlobItem in objIListBlobItems)
                {
                    if (objIListBlobItem.GetType() == typeof(CloudBlockBlob))
                    {
                        objCloudBlockBlob = (CloudBlockBlob)objIListBlobItem;
                        Console.WriteLine(FORMAT2, objCloudBlockBlob.Name, objIListBlobItem.GetType().Name);
                    }
                    else if (objIListBlobItem.GetType() == typeof(CloudBlobDirectory))
                    {
                        objCloudBlobDirectory = (CloudBlobDirectory)objIListBlobItem;

                        BlobRequestOptions objBlobRequestOptions = new BlobRequestOptions();
                        objBlobRequestOptions.DisableContentMD5Validation = true;

                        objIListBlobItems1 = objCloudBlobDirectory.ListBlobs(false, BlobListingDetails.None, /*objBlobRequestOptions*/null, null);
                        int count = objIListBlobItems1.Count();
                        if (count == 1)
                        {
                            objIListBlobItemHeader = objIListBlobItems1.ElementAt(count - 1);
                            Console.WriteLine(FORMAT3, objCloudBlobDirectory.Prefix, objIListBlobItem.GetType().Name, count.ToString(), string.Empty, string.Empty, objIListBlobItemHeader.Uri.Segments[3]);
                        }
                        else if (count == 2)
                        {
                            objIListBlobItemHeader = objIListBlobItems1.ElementAt(count - 1);
                            Console.WriteLine(FORMAT3, objCloudBlobDirectory.Prefix, objIListBlobItem.GetType().Name, count.ToString(), objIListBlobItems1.First().Uri.Segments[3], string.Empty, objIListBlobItemHeader.Uri.Segments[3]);
                        }
                        else if (count > 2)
                        {
                            objIListBlobItemLast = objIListBlobItems1.ElementAt(count - 2);
                            objIListBlobItemHeader = objIListBlobItems1.ElementAt(count - 1);
                            Console.WriteLine(FORMAT3, objCloudBlobDirectory.Prefix, objIListBlobItem.GetType().Name, count.ToString(), objIListBlobItems1.First().Uri.Segments[3], objIListBlobItemLast.Uri.Segments[3], objIListBlobItemHeader.Uri.Segments[3]);
                        } 
                        //foreach (IListBlobItem objIListBlobItem1 in objIListBlobItems1)
                        //{
                        //    Console.WriteLine("\t{0}", objIListBlobItem1.Uri.Segments[3]);
                        //}

                        //check IEnumerable<IListBlobItem> in each CloudBlobDirectory
                        if (!objCloudBlobDirectory.Prefix.StartsWith("ADI3"))
                        {
                            //check for missing fragblob for each quality level of video only
                            CheckForMissingFragblobs(objCloudBlobDirectory, objIListBlobItems1, ref objIList_MissingFragblob);
                            //check fragblob write delay
                            if (bool.Parse(System.Configuration.ConfigurationManager.AppSettings["CheckFragblobArchiveDelay"]))
                            {
                                CheckFragblobLastModified(objCloudBlobDirectory, objIListBlobItems1);
                            }
                        }
                        else
                        {
                            //ad markers (SCTE35 messages)
                            CheckAdMarkers(objIListBlobItems1, sasLocator);
                        }
                    }
                    else if (objIListBlobItem.GetType() == typeof(CloudPageBlob))
                    {
                        objCloudPageBlob = (CloudPageBlob)objIListBlobItem;
                        Console.WriteLine(FORMAT2, objCloudPageBlob.Uri.ToString(), objIListBlobItem.GetType().Name);
                    }
                }
            }

            PrintAllMissingFlagblobs(objIList_MissingFragblob);
        }

        public static void CheckForMissingFragblobs(CloudBlobDirectory objCloudBlobDirectory, IEnumerable<IListBlobItem> objIListBlobItems, ref IList<MissingFragblob> objIList_MissingFragblob)
        {
            long GOP_SIZE = long.Parse(System.Configuration.ConfigurationManager.AppSettings["GOPSize"]);
            long hns, hns_previous = 0;
            MissingFragblob objMissingFragblob;

            //collect all fragblob timestamps
            List<long> objList_long = new List<long>();
            foreach (IListBlobItem objIListBlobItem in objIListBlobItems)
            {
                if (objIListBlobItem.Uri.Segments[3].ToLower() != "header")
                {
                    hns = long.Parse(objIListBlobItem.Uri.Segments[3]);
                    objList_long.Add(hns);
                }
            }
            //sort timestamps
            var sortedList = from n in objList_long orderby n ascending select n;
            long[] timestamps = sortedList.ToArray<long>();
            //check gaps
            foreach (long timestamp in timestamps)
            {
                if (hns_previous > 0)
                {
                    if (timestamp - hns_previous > GOP_SIZE + 800000)
                    {
                        objMissingFragblob = new MissingFragblob();
                        objMissingFragblob.CloudBobDirectoryPrefix = objCloudBlobDirectory.Prefix;
                        objMissingFragblob.PreviousFragblob = hns_previous;
                        objMissingFragblob.NextFragblob = timestamp;
                        objIList_MissingFragblob.Add(objMissingFragblob);
                    }
                }
                hns_previous = timestamp;
            }

            //print out first MAX timestamps
            //for (int i = 0; i < timestamps.Length; i++)
            //{
            //    if (timestamps[i] >= 13904286580072895 && timestamps[i] < 13904287180072895)
            //    {
            //        Console.WriteLine("{0,-4} {1}", i.ToString(), timestamps[i].ToString());
            //    }
            //}
        }

        public static void CheckFragblobLastModified(CloudBlobDirectory objCloudBlobDirectory, IEnumerable<IListBlobItem> objIListBlobItems)
        {
            CloudBlockBlob objCloudBlockBlob;
            IList<CloudBlockBlob> objIList_CloudBlockBlob = new List<CloudBlockBlob>();

            //collect all CloudBlockBlob
            foreach (IListBlobItem objIListBlobItem in objIListBlobItems)
            {
                if (objIListBlobItem.Uri.Segments[3].ToLower() != "header")
                {
                    objCloudBlockBlob = (CloudBlockBlob)objIListBlobItem;
                    //objCloudBloclBlob.FetchAttributes();
                    objIList_CloudBlockBlob.Add(objCloudBlockBlob);
                }
            }

            //sort by CloudBlockBlob.Properties.LastModified (sorting is not necessary, but as a precaution)
            var sorted = from b in objIList_CloudBlockBlob orderby b.Properties.LastModified.Value ascending select b;

            //print out abnormal fragblob write time
            DateTimeOffset previous = DateTime.MinValue, current;
            TimeSpan span;
            string FORMAT = "{0,-90}{1,-5}";
            int counter = 0;
            //time range parameters used which are different for different GOP size
            int t0 = 1000, t1 = 2500, t2 = 4000;   //milliseconds
            int gop = int.Parse(System.Configuration.ConfigurationManager.AppSettings["GOPSize"]);
            switch (gop)
            {
                case 20000000:
                    t0 = 1000;
                    t1 = 2500;
                    t2 = 4000;
                    break;
                case 60000000:
                    t0 = 1000;
                    t1 = 7000;
                    t2 = 12000;
                    break;
                default:
                    break;
            }

            //print out fragblob archive delay or burst
            foreach (CloudBlockBlob objCloudBlockBlob1 in sorted)
            {
                current = objCloudBlockBlob1.Properties.LastModified.Value;
                if (previous > DateTime.MinValue)
                {
                    span = (TimeSpan)(current - previous);
                    if (span.TotalMilliseconds <= t0)
                    {
                        counter++;
                        ColorConsole.WriteLine(string.Format(FORMAT, objCloudBlockBlob1.Uri.AbsolutePath, (span.TotalMilliseconds / 1000).ToString().PadLeft(3)), ConsoleColor.ForegroundBlue);
                    }
                    else if (span.TotalMilliseconds > t1 && span.TotalMilliseconds <= t2)
                    {
                        counter++;
                        ColorConsole.WriteLine(string.Format(FORMAT, objCloudBlockBlob1.Uri.AbsolutePath, (span.TotalMilliseconds / 1000).ToString().PadLeft(3)), ConsoleColor.ForegroundYellow);
                        totalDelay = totalDelay + (long) (span.TotalMilliseconds - 6000);
                    }
                    else if (span.TotalMilliseconds > t2)
                    {
                        counter++;
                        ColorConsole.WriteLine(string.Format(FORMAT, objCloudBlockBlob1.Uri.AbsolutePath, (span.TotalMilliseconds / 1000).ToString().PadLeft(3)), ConsoleColor.ForegroundRed);
                        totalDelay = totalDelay + (long)(span.TotalMilliseconds - 6000);
                    }
                    else
                    {
                        //counter++;
                        //Console.WriteLine(FORMAT, objCloudBlockBlob1.Uri.AbsolutePath, (span.TotalMilliseconds / 1000).ToString().PadLeft(3));
                    }
                }
                previous = current;
            }

            //if (counter > 100)
            //{
            //    Console.WriteLine("Click any key to continue ...");
            //    Console.ReadKey();
            //}

            Console.WriteLine("Total archive delay = {0} second", (totalDelay/1000).ToString());
        }

        public static void CheckAdMarkers(IEnumerable<IListBlobItem> objIListBlobItems, ILocator objILocator_Sas)
        {
            string uri;
            string scte;
            ColorConsole.WriteLine(string.Format("\t{0,-82}", "SCTE-35 XML in Ad Marker"), ConsoleColor.ForegroundGreen);
            foreach (IListBlobItem objIListBlobItem in objIListBlobItems)
            {
                if (!objIListBlobItem.Uri.AbsolutePath.Contains("header"))
                {
                    scte = GetFragblobToString(objIListBlobItem);
                    if (scte.IndexOf("<AcquiredSignal") > 0)
                    {
                        scte = scte.Substring(scte.IndexOf("<AcquiredSignal"));
                        Console.WriteLine("\t" + scte.TrimEnd(Environment.NewLine.ToCharArray()));
                    }
                }
            }

            ColorConsole.WriteLine(string.Format("\t{0,-82}", "SAS URI for Ad Markers"), ConsoleColor.ForegroundGreen);
            foreach (IListBlobItem objIListBlobItem in objIListBlobItems)
            {
                if (!objIListBlobItem.Uri.AbsolutePath.Contains("header"))
                {
                    uri = GetSasUriOfIListBlobItem(objIListBlobItem, objILocator_Sas);
                    Console.WriteLine("\t" + uri);
                }
            }
        }

        //objIListBlobItem is a fragblob in storage. Retrieve it and convert to string.
        public static string GetFragblobToString(IListBlobItem objIListBlobItem)
        {
            System.IO.MemoryStream objMemoryStream;
            System.IO.StreamReader objStreamReader;
            objMemoryStream = new System.IO.MemoryStream();
            objIListBlobItem.Parent.ServiceClient.GetBlobReferenceFromServer(objIListBlobItem.StorageUri).DownloadToStream(objMemoryStream);
            //objIListBlobItem.Parent.ServiceClient.GetBlobReferenceFromServer(objIListBlobItem.StorageUri).DownloadToFile("ztest.txt", System.IO.FileMode.Append);
            objMemoryStream.Seek(0, System.IO.SeekOrigin.Begin);
            objStreamReader = new System.IO.StreamReader(objMemoryStream, System.Text.Encoding.ASCII);
            string scte = objStreamReader.ReadToEnd();

            return scte;
        }

        //build SAS URL for an IListBlobItem inside a CloudBlobDirectory
        public static string GetSasUriOfIListBlobItem(IListBlobItem objIListBlobItem, ILocator objILocator)
        {
            Uri uri = new Uri(objILocator.BaseUri);
            string sasUri = string.Format("https://{0}{1}{2}", uri.Host, objIListBlobItem.Uri.AbsolutePath, objILocator.ContentAccessComponent);
            return sasUri;
        }

        public static void PrintAllMissingFlagblobs(IList<MissingFragblob> objIList_MissingFragblob)
        {
            string FORMAT = "{0,-30}{1,-30}{2,-30}{3,-30}{4,-30}";
            double span;
            DateTime time;
            if (objIList_MissingFragblob != null && objIList_MissingFragblob.Count > 0)
            {
                ColorConsole.WriteLine(string.Format(FORMAT, "Prefix", "Current Fragblob", "Next Fragblob", "Time Difference", "Time (UTC)"), ConsoleColor.ForegroundGreen);
                //write to file
                if (objStreamWriter != null)
                {
                    objStreamWriter.BaseStream.Seek(0, System.IO.SeekOrigin.End);
                    objStreamWriter.WriteLine(string.Format(FORMAT, "Prefix", "Current Fragblob", "Next Fragblob", "Time Difference", "Time (UTC)"));
                }

                //sort by time
                var sorted = from f in objIList_MissingFragblob orderby f.PreviousFragblob select f;
                foreach (MissingFragblob objMissingFragblob in sorted)
                {
                    span = (objMissingFragblob.NextFragblob - objMissingFragblob.PreviousFragblob)/10000000.0;
                    time = HnsToUtc(objMissingFragblob.PreviousFragblob);
                    ColorConsole.WriteLine(string.Format(FORMAT, objMissingFragblob.CloudBobDirectoryPrefix, objMissingFragblob.PreviousFragblob.ToString(), objMissingFragblob.NextFragblob.ToString(), string.Format("{0:00.00}", span), time.ToString("MM/dd/yyyy HH:mm:ss")), ConsoleColor.ForegroundRed);

                    //write to file
                    if (objStreamWriter != null)
                    {
                        objStreamWriter.BaseStream.Seek(0, System.IO.SeekOrigin.End);
                        objStreamWriter.WriteLine(string.Format(FORMAT, objMissingFragblob.CloudBobDirectoryPrefix, objMissingFragblob.PreviousFragblob.ToString(), objMissingFragblob.NextFragblob.ToString(), string.Format("{0:00.00}", span), time.ToString("MM/dd/yyyy HH:mm:ss")));
                    }
                }
            }
        }

        public static void ListServerManifest(string sasUrl)
        {
            XmlDocument objXmlDocument = DownloadToXmlDocument(sasUrl);
            if (objXmlDocument != null)
            {
                string FORMAT = "{0,-30}{1,-30}{2,-60}";
                ColorConsole.WriteLine(string.Format(FORMAT, "Track Type", "systemBitrate", "src"), ConsoleColor.ForegroundGreen);
                
                //XPath query for <switch>
                XmlNamespaceManager objXmlNamespaceManager = new XmlNamespaceManager(objXmlDocument.NameTable);
                objXmlNamespaceManager.AddNamespace("ns", "http://www.w3.org/2001/SMIL20/Language");
                XmlNode objXmlNode = objXmlDocument.DocumentElement.SelectSingleNode("//ns:switch", objXmlNamespaceManager);

                //collect and sort TrackInfo
                IList<TrackInfo> objIList_TrackInfo = new List<TrackInfo>();
                TrackInfo objTrackInfo;
                foreach (XmlNode objXmlNode1 in objXmlNode.ChildNodes)
                {
                    objTrackInfo               = new TrackInfo();
                    objTrackInfo.Type          = objXmlNode1.Name;
                    objTrackInfo.src           = objXmlNode1.Attributes["src"].Value;
                    objTrackInfo.systemBitrate = int.Parse(objXmlNode1.Attributes["systemBitrate"].Value);
                    objIList_TrackInfo.Add(objTrackInfo);
                }
                var sorted = from t in objIList_TrackInfo orderby t.systemBitrate ascending select t;

                //print
                foreach (TrackInfo objTrackInfo1 in sorted)
                {
                    Console.WriteLine(FORMAT, objTrackInfo1.Type, objTrackInfo1.systemBitrate.ToString().PadLeft(7), objTrackInfo1.src);
                }

                //XPath query for <meta>
                ColorConsole.WriteLine(string.Format(FORMAT, "meta name", "meta content", ""), ConsoleColor.ForegroundGreen);
                objXmlNode = objXmlDocument.DocumentElement.SelectSingleNode("//ns:head", objXmlNamespaceManager);
                foreach (XmlNode objXmlNode2 in objXmlNode.ChildNodes)
                {
                    Console.WriteLine(FORMAT, objXmlNode2.Attributes["name"].Value, objXmlNode2.Attributes["content"].Value, "");
                }
            }
        }

        public static IAsset GetAsset(string assetId, CloudMediaContext objCloudMediaContext)
        {
            // Use a LINQ Select query to get an asset.
            var assetInstance = from a in objCloudMediaContext.Assets where a.Id == assetId select a;
            // Reference the asset as an IAsset.
            IAsset objIAsset = assetInstance.FirstOrDefault();

            return objIAsset;
        }

        public static IProgram GetProgramFromAssetId(string assetId, CloudMediaContext objCloudMediaContext)
        {
            IProgram objIProgram = objCloudMediaContext.Programs.Where(p => p.AssetId == assetId).SingleOrDefault();
            return objIProgram;
        }

        public static void ListProgramChannel(string assetId, CloudMediaContext objCloudMediaContext)
        {
            string FORMAT = "{0,-30}{1,-30}";
            IProgram objIProgram = GetProgramFromAssetId(assetId, objCloudMediaContext);
            if (objIProgram != null && objIProgram.Channel != null)
            {
                IChannel objIChannel = objIProgram.Channel;

                ColorConsole.WriteLine(string.Format(FORMAT, "Attribute", "Value"), ConsoleColor.ForegroundGreen);
                Console.WriteLine(FORMAT, "Program Name",    objIProgram.Name);
                Console.WriteLine(FORMAT, "Program Desc",    objIProgram.Description);
                Console.WriteLine(FORMAT, "Program State",   objIProgram.State.ToString());
                Console.WriteLine(FORMAT, "Manifest Name",   objIProgram.ManifestName);
                Console.WriteLine(FORMAT, "Program Created", objIProgram.Created.ToString("MM/dd/yyyy HH:mm:ss"));  //UTC
                Console.WriteLine(FORMAT, "Channel Name",    objIChannel.Name);
                Console.WriteLine(FORMAT, "Channel Desc",    objIChannel.Description);
                Console.WriteLine(FORMAT, "Preview URL",     objIChannel.Preview.Endpoints[0].Url.ToString());
                Console.WriteLine(FORMAT, "Ingest URL",      objIChannel.Input.Endpoints[0].Url.ToString());
            }
        }

        public static string GetIsmFileName(string assetId, CloudMediaContext objCloudMediaContext)
        {
            IAsset objIAsset = GetAsset(assetId, objCloudMediaContext);
            IAssetFile objIAssetFile = objIAsset.AssetFiles.Where(f => f.Name.EndsWith(".ism")).FirstOrDefault();

            if (objIAssetFile != null)
                return objIAssetFile.Name;
            else
                return string.Empty;
        }

        #endregion

        #region GOP/Packing ratio settings

        public static void UpdateGopSettings(IChannel objIChannel, CloudMediaContext objCloudMediaContext)
        {
            objIChannel.Output = new ChannelOutput
            {
                Hls = new ChannelOutputHls
                {
                     FragmentsPerSegment = 1
                }
            };
            
            objIChannel.Update();

            objIChannel = GetChannel(objIChannel.Name, objCloudMediaContext);

            GetGopSettings(objIChannel);
        }

        public static void GetGopSettings(IChannel objIChannel)
        {
            Console.WriteLine(@"IChannel.Output.Hls.FragmentsPerSegment: {0}", objIChannel.Output.Hls.FragmentsPerSegment.Value.ToString());
        }

        public static void ListAllGopSettings(CloudMediaContext objCloudMediaContext)
        {
            string keyframeInterval, fragmentsPerSegment;
            try
            {
                foreach (IChannel objIChannel in objCloudMediaContext.Channels)
                {
                    keyframeInterval = objIChannel.Input.KeyFrameInterval.HasValue ? objIChannel.Input.KeyFrameInterval.Value.Seconds.ToString() : "null";
                    fragmentsPerSegment = objIChannel.Output.Hls.FragmentsPerSegment.HasValue ? objIChannel.Output.Hls.FragmentsPerSegment.Value.ToString() : "null";
                    Console.WriteLine(@"Channel: {0} - IChannel.Output.Hls.FragmentsPerSegment = {1}, IChannel.Input.KeyFrameInterval = {2}", objIChannel.Name, fragmentsPerSegment, keyframeInterval);
                }
            }
            catch{}
        }

        public static void UpdateGopSettingsForAllChannels(CloudMediaContext objCloudMediaContext)
        {
            foreach (IChannel objIChannel in objCloudMediaContext.Channels)
            {
                UpdateGopSettings(objIChannel, objCloudMediaContext);
            }
        }

        #endregion

        #region SecuritySettings: Origin, Ingest and Preview

        //Channel: both ingest and preview
        public static void UpdateAllChannelSecuritySettings(CloudMediaContext objCloudMediaContext)
        {
            IChannel objIChannel;

            foreach (string channelName in CHANNEL_NAMES)
            {
                objIChannel = GetChannel(channelName, objCloudMediaContext);
                //warning: NOT setting objIChannel.Input.Endpoints and objIChannel.Preview.Endpoints to be null, which already have values
                objIChannel.Input.AccessControl.IPAllowList = Program.CreateIPRangeList();
                objIChannel.Preview.AccessControl.IPAllowList = Program.CreateIPRangeList();
                objIChannel.Output = new ChannelOutput
                {
                    Hls = new ChannelOutputHls
                    {
                         FragmentsPerSegment = 1
                    }
                };
                objIChannel.SendUpdateOperation();
                Console.WriteLine("Updating channel {0} preview, input and output settings", channelName);
            }
        }

        public static void ListAllChannelOriginSecuritySettings(CloudMediaContext objCloudMediaContext)
        {
            string FORMAT = "{0,-25}{1,-50}{2,-50}";
            var channels = objCloudMediaContext.Channels.ToList().OrderBy(o => o.Name);

            //IChannel.Preview
            Console.WriteLine("IChannel.Preview.AccessControl");
            foreach (IChannel objIChannel in channels)
            {
                Console.WriteLine("Channel: {0}", objIChannel.Name);
                if (objIChannel.Preview != null && objIChannel.Preview.AccessControl != null && objIChannel.Preview.AccessControl.IPAllowList != null)
                {
                    foreach (IPRange objIPRange in objIChannel.Preview.AccessControl.IPAllowList)
                    {
                        Console.WriteLine(FORMAT, string.Empty, objIPRange.Name, string.Format("{0}/{1}", objIPRange.Address.ToString(), objIPRange.SubnetPrefixLength.Value.ToString()));
                    }
                }
            }

            //IChannel.Input
            Console.WriteLine("IChannel.Input.AccessControl");
            foreach (IChannel objIChannel in channels)
            {
                Console.WriteLine("Channel: {0}", objIChannel.Name);
                if (objIChannel.Input != null && objIChannel.Input.AccessControl != null && objIChannel.Input.AccessControl.IPAllowList != null)
                {
                    foreach (IPRange objIPRange in objIChannel.Input.AccessControl.IPAllowList)
                    {
                        Console.WriteLine(FORMAT, string.Empty, objIPRange.Name, string.Format("{0}/{1}", objIPRange.Address.ToString(), objIPRange.SubnetPrefixLength.Value.ToString()));
                    }
                }
            }

            //IStreamingEndpoint.AccessControl
            Console.WriteLine("IStreamingEndpoint.AccessControl");
            var origins = objCloudMediaContext.StreamingEndpoints.ToList().OrderBy(o => o.Name);
            foreach (IStreamingEndpoint objIStreamingEndpoint in origins)
            {
                Console.WriteLine("Origin: {0}", objIStreamingEndpoint.Name);
                if (objIStreamingEndpoint.AccessControl != null && objIStreamingEndpoint.AccessControl.IPAllowList != null)
                {
                    foreach (IPRange objIPRange in objIStreamingEndpoint.AccessControl.IPAllowList)
                    {
                        Console.WriteLine(FORMAT, string.Empty, objIPRange.Name, string.Format("{0}/{1}", objIPRange.Address.ToString(), objIPRange.SubnetPrefixLength.Value.ToString()));
                    }
                }
            }
        }

        //Origin
        public static void UpdateAllOriginSecuritySettings(CloudMediaContext objCloudMediaContext)
        {
            IStreamingEndpoint objIStreamingEndpoint;

            foreach (string channelName in CHANNEL_NAMES)
            {
                objIStreamingEndpoint = GetOrigin(channelName, objCloudMediaContext);
                objIStreamingEndpoint.AccessControl = new StreamingEndpointAccessControl
                {
                    IPAllowList = Program.CreateIPRangeList()
                };
                objIStreamingEndpoint.SendUpdateOperation();
                Console.WriteLine("Updating origin {0} IPv4AllowList", channelName);
            }
        }

        public static IChannel GetChannel(string channelName, CloudMediaContext objCloudMediaContext)
        {
            IChannel objIChannel = objCloudMediaContext.Channels.Where(c => c.Name == channelName).FirstOrDefault();
            //Console.WriteLine(string.Format("IChannel.IngestUrl = {0}", objIChannel.IngestUrl));
            //Console.WriteLine(string.Format("IChannel.PreviewUrl = {0}", objIChannel.PreviewUrl));
            return objIChannel;
        }

        public static IStreamingEndpoint GetOrigin(string originName, CloudMediaContext objCloudMediaContext)
        {
            IStreamingEndpoint objIStreamingEndpoint = objCloudMediaContext.StreamingEndpoints.Where(o => o.Name == originName).FirstOrDefault();
            return objIStreamingEndpoint;
        }

        #endregion

        #region Locator and URI
        
        //origin URL -> locator GUID -> Asset or SAS locator
        public static string GetLocatorGuidFromOriginUrl(string originUrl)
        {
            Uri uri = new Uri(originUrl);
            string locatorGuid = uri.Segments[1].TrimEnd('/');  //origin URL format
            Guid guid;
            if (!Guid.TryParse(locatorGuid, out guid))
            {
                locatorGuid = uri.Segments[2].TrimEnd('/');     //edge URL format
                //Console.WriteLine("This is an edge URL. Locator GUID = {0}", locatorGuid);
            }
            else
            {
                //Console.WriteLine("This is an origin URL. Locator GUID = {0}", locatorGuid);
            }

            return locatorGuid;
        }

        public static IAsset GetAssetFromOriginUrl(string url, CloudMediaContext objCloudMediaContext)
        {
            string originLocatorGuid = GetLocatorGuidFromOriginUrl(url);
            string originLocatorId = "nb:lid:UUID:" + originLocatorGuid;
            ILocator objILocator = objCloudMediaContext.Locators.Where(l => l.Id == originLocatorId).FirstOrDefault();
            IAsset objIAsset = null;
            if (objILocator != null)
            {
                objIAsset = objILocator.Asset;
            }
            return objIAsset;
        }

        public static void ListLocators(string url, CloudMediaContext objCloudMediaContext)
        {
            string FORMAT_STRING = "{0,-60}{1,-30}{2,-30}{3,-30}{4,-30}{5,-30}";
            IAsset objIAsset;

            objIAsset = GetAssetFromOriginUrl(url, objCloudMediaContext); //either origin or edge URL
            string start;
            ColorConsole.WriteLine(string.Format(FORMAT_STRING, "ILocator.Id", "ILocator.Name", "ILocator.Type", "StartTime", "Expiration", "Duration"), ConsoleColor.ForegroundGreen);
            foreach (ILocator objILocator in objIAsset.Locators.OrderBy(l => l.ExpirationDateTime))
            {
                start = objILocator.StartTime.HasValue ? objILocator.StartTime.Value.ToString("MM/dd/yyyy HH:mm:ss") : string.Empty;
                Console.WriteLine(FORMAT_STRING, objILocator.Id, objILocator.Name, objILocator.Type.ToString(), start, objILocator.ExpirationDateTime.ToString("MM/dd/yyyy HH:mm:ss"), objILocator.AccessPolicy.Duration.TotalDays.ToString());
            }
        }


        //e.g. https://originlongevitytestusb.blob.core.windows.net/asset-7cdbaf72-0e40-4b3e-9755-98d831a4ec25/test01.xml?sv=2012-02-12&se=2014-03-14T02%3A51%3A52Z&sr=c&si=63d955cb-606b-4026-9262-dbb62b40931b&sig=u4384d78k2zQKrWm6vyM28GXkRE8DqpH7ZNst4qxqD8%3D
        public static string GetSasUriForFile(ILocator objILocator, string filename)
        {
            return string.Format("{0}/{1}{2}", objILocator.BaseUri, filename, objILocator.ContentAccessComponent);
        }

        public static ILocator GetSasLocator(string assetId, CloudMediaContext objCloudMediaContext)
        {
            IAsset objIAsset = GetAsset(assetId, objCloudMediaContext);
            ILocator objILocator = null;

            var assetLocators = objIAsset.Locators.ToList();
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
                            objILocator = assetLocator;
                            break;
                        }
                        else
                        {
                            assetLocator.Delete();  //delete expired SAS locator. No more than 5 total locators is allowed.
                        }
                    }  
                }
            }


            if (objILocator == null)
            {

                IAccessPolicy accessPolicy = objCloudMediaContext.AccessPolicies.Where(ap => ap.Name == "Read_List_Write_Access_Policy").FirstOrDefault();
                if (accessPolicy == null)
                {
                    TimeSpan duration = TimeSpan.FromDays(100);
                    accessPolicy = objCloudMediaContext.AccessPolicies.Create("Read_List_Write_Access_Policy", duration,
                                                                              AccessPermissions.Read | AccessPermissions.List | AccessPermissions.Write);
                }
                objILocator = objCloudMediaContext.Locators.CreateLocator(LocatorType.Sas, objIAsset, accessPolicy);
            }

            // Return the locator.
            //Console.WriteLine("SAS locator: " + objILocator.Path);
            //Console.WriteLine("Streaming asset Id: " + objIAsset.Id);
            return objILocator;
        }

        public static ILocator GetOriginLocator(string assetId, CloudMediaContext objCloudMediaContext)
        {
            IAsset objIAsset = GetAsset(assetId, objCloudMediaContext);
            ILocator objILocator = null;

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


            if (objILocator == null)
            {

                IAccessPolicy accessPolicy = objCloudMediaContext.AccessPolicies.Where(ap => ap.Name == "Origin_Locator_Access_Policy").FirstOrDefault();
                if (accessPolicy == null)
                {
                    TimeSpan duration = TimeSpan.FromDays(100);
                    accessPolicy = objCloudMediaContext.AccessPolicies.Create("Origin_Locator_Access_Policy", duration, AccessPermissions.Read);
                }
                objILocator = objCloudMediaContext.Locators.CreateLocator(LocatorType.OnDemandOrigin, objIAsset, accessPolicy);
            }

            //display URLs, etc.
            //string url = GetOriginUri(objILocator);

            return objILocator;
        }

        public static string GetOriginUri(ILocator objILocator)
        {
            var theManifest = from f in objILocator.Asset.AssetFiles where f.Name.EndsWith(".ism") select f;
            // Cast the reference to a true IAssetFile type. 
            IAssetFile objIAssetFile = theManifest.First();
            string url = string.Format("{0}{1}/manifest", objILocator.Path, objIAssetFile.Name);

            //print origin URLs
            //Console.WriteLine("\tManifest file name = {0}", objIAssetFile.Name);
            //Console.WriteLine("\tHSS: {0}{1}/manifest", objILocator.Path, objIAssetFile.Name);
            //Console.WriteLine("\tHLS: {0}{1}/manifest(format=m3u8-aapl)", objILocator.Path, objIAssetFile.Name);
            //Console.WriteLine("\tHDS: {0}{1}/manifest(format=f4m-f4f).f4m", objILocator.Path, objIAssetFile.Name);
            //Console.WriteLine("\tAssetID = {0}", objILocator.Asset.Id);
            //Console.WriteLine("****************************************************");

            return url;
        }

        public static string FromOriginUriToSasUri(string originUri, CloudMediaContext objCloudMediaContext)
        {
            Uri uri = new Uri(originUri);
            IAsset objIAsset = GetAssetFromOriginUrl(originUri, objCloudMediaContext);
            ILocator objILocator = GetSasLocator(objIAsset.Id, objCloudMediaContext);
            string manifestFileName = uri.Segments[2].TrimEnd('/');
            string sasUri = GetSasUriForFile(objILocator, manifestFileName);
            return sasUri;
        }

        #endregion

        #region Timecode, HSN, start/end, manifest info

        public static void ListManifestInfo(string url)
        {
            string FORMAT_MANIFEST   = "{0,-30}{1,-30}";
            string FORMAT_STREAMINFO = "{0,-30}{1,-30}{2,-30}{3,-30}{4,-30}"; 
            SmoothStreamingManifestParser objSmoothStreamingManifestParser = GetSmoothStreamingManifestParser(url);
            
            if (objSmoothStreamingManifestParser != null)
            {
                SmoothStreamingManifestGenerator.Models.ManifestInfo objManifestInfo = objSmoothStreamingManifestParser.ManifestInfo;
                if (objManifestInfo != null)
                {
                    IList<SmoothStreamingManifestGenerator.Models.StreamInfo> objIList_StreamInfo = objManifestInfo.Streams;
                    IList<SmoothStreamingManifestGenerator.Models.QualityLevel> objIList_QualityLevel;
                    IList<SmoothStreamingManifestGenerator.Models.QualityLevel> objIList_VideoQualityLevel = null;
                    StringBuilder sb;
                    IDictionary<string, string> objIDictionary_Attributes;

                    ColorConsole.WriteLine(string.Format(FORMAT_MANIFEST, "Manifest Property", "Value"), ConsoleColor.ForegroundGreen);
                    Console.WriteLine(FORMAT_MANIFEST, "DvrWindowLength", objManifestInfo.DvrWindowLength.ToString());
                    Console.WriteLine(FORMAT_MANIFEST, "IsLive", objManifestInfo.IsLive.ToString());
                    Console.WriteLine(FORMAT_MANIFEST, "LookAheadFragmentCount", objManifestInfo.LookAheadFragmentCount.ToString());
                    Console.WriteLine(FORMAT_MANIFEST, "ManifestDuration", objManifestInfo.ManifestDuration.ToString());
                    Console.WriteLine(FORMAT_MANIFEST, "MajorVersion", objManifestInfo.MajorVersion.ToString());
                    Console.WriteLine(FORMAT_MANIFEST, "MinorVersion", objManifestInfo.MinorVersion.ToString());
                    if (objIList_StreamInfo != null && objIList_StreamInfo.Count() > 0 && objIList_StreamInfo[0].Chunks.Count > 0)
                    {
                        Console.WriteLine(FORMAT_MANIFEST, "Chunk.Duration", objIList_StreamInfo[0].Chunks[0].Duration.ToString());
                    }

                    if (objIList_StreamInfo != null && objIList_StreamInfo.Count() > 0)
                    {
                        ColorConsole.WriteLine(string.Format(FORMAT_STREAMINFO, "StreamType", "IsSparseStream", "QualityLevels", "Chunks", "Attributes/CustomAttributes"), ConsoleColor.ForegroundGreen);
                        foreach (SmoothStreamingManifestGenerator.Models.StreamInfo objStreamInfo in objIList_StreamInfo)
                        {
                            //QualityLevel attributes in each stream
                            sb = new StringBuilder();
                            objIList_QualityLevel = objStreamInfo.QualityLevels;
                            foreach (SmoothStreamingManifestGenerator.Models.QualityLevel objQualityLevel in objIList_QualityLevel)
                            {
                                objIDictionary_Attributes = objQualityLevel.Attributes;
                                foreach (string key in objIDictionary_Attributes.Keys)
                                {
                                    sb.AppendFormat("{0}: {1}; ", key, objIDictionary_Attributes[key]);
                                }
                                objIDictionary_Attributes = objQualityLevel.CustomAttributes;
                                foreach (string key in objIDictionary_Attributes.Keys)
                                {
                                    sb.AppendFormat("{0}: {1}; ", key, objIDictionary_Attributes[key]);
                                }
                            }

                            //each stream
                            switch (objStreamInfo.StreamType.ToLower())
                            {
                                case "text":
                                    Console.WriteLine(FORMAT_STREAMINFO, objStreamInfo.StreamType, objStreamInfo.IsSparseStream.ToString(), objStreamInfo.QualityLevels.Count.ToString(), objStreamInfo.Chunks.Count.ToString() + " (ad markers)", sb.ToString());
                                    break;
                                case "video":
                                    //keep video quality level
                                    objIList_VideoQualityLevel = objIList_QualityLevel;
                                    Console.WriteLine(FORMAT_STREAMINFO, objStreamInfo.StreamType, objStreamInfo.IsSparseStream.ToString(), objStreamInfo.QualityLevels.Count.ToString(), objStreamInfo.Chunks.Count.ToString(), "(See below for all quality levels)");
                                    break;
                                default:
                                    Console.WriteLine(FORMAT_STREAMINFO, objStreamInfo.StreamType, objStreamInfo.IsSparseStream.ToString(), objStreamInfo.QualityLevels.Count.ToString(), objStreamInfo.Chunks.Count.ToString(), sb.ToString());
                                    break;
                            }

                        }
                    }

                    //list video quality level attributes
                    if (objIList_VideoQualityLevel != null)
                    {
                        string header = string.Empty;
                        objIDictionary_Attributes = objIList_VideoQualityLevel[0].Attributes;
                        foreach (string key in objIDictionary_Attributes.Keys)
                        {
                            if (objIDictionary_Attributes[key].Length < 80)
                            {
                                header += string.Format("{0,-15}", key);
                            }
                            else
                            {
                                header += string.Format("{0,-120}", key);
                            }
                        }
                        ColorConsole.WriteLine(header, ConsoleColor.ForegroundGreen);

                        foreach (SmoothStreamingManifestGenerator.Models.QualityLevel objQualityLevel in objIList_VideoQualityLevel)
                        {
                            objIDictionary_Attributes = objQualityLevel.Attributes;
                            foreach (string key in objIDictionary_Attributes.Keys)
                            {
                                if (objIDictionary_Attributes[key].Length < 80)
                                {
                                    if (key.ToLower() == "bitrate")
                                    {
                                        Console.Write("{0,-15}", objIDictionary_Attributes[key].PadLeft(7));
                                    }
                                    else
                                    {
                                        Console.Write("{0,-15}", objIDictionary_Attributes[key]);
                                    }
                                }
                                else
                                {
                                    Console.Write("{0,-120}", objIDictionary_Attributes[key]);
                                }
                            }
                            Console.Write(Environment.NewLine);
                        }
                    }
                }
                else
                {
                    ColorConsole.WriteLine("ManifestInfo is null", ConsoleColor.ForegroundRed);
                }
                
            }
        }

        public static void ListVideoTimes(string url)
        {
            SmoothStreamingManifestParser objSmoothStreamingManifestParser = GetSmoothStreamingManifestParser(url);
            if (objSmoothStreamingManifestParser == null) return;

            ulong startPosition = GetStartPositionFromManifest(objSmoothStreamingManifestParser);
            ulong endPosition = 0;   
            if (objSmoothStreamingManifestParser.ManifestInfo.IsLive)
            {
                //for live, SmoothStreamingManifestParser.ManifestInfo.ManifestDuration always returns 36000000000 (1 Hr)
                endPosition = (ulong) UtcToHns(DateTime.UtcNow);
            }
            else
            {
                endPosition = startPosition + objSmoothStreamingManifestParser.ManifestInfo.ManifestDuration;
            }
            long duration = (long) (endPosition - startPosition);
            TimeSpan objTimeSpan = TimeSpan.FromTicks(duration);
            DateTime start = HnsToUtc((long) startPosition);
            DateTime end   = HnsToUtc((long) endPosition);

            string FORMAT = "{0,-30}{1,-30}{2,-30}{3,-30}";
            ColorConsole.WriteLine(string.Format(FORMAT, "Type", "Start (UTC)", "End/Current Live Point (UTC)", "Duration"), ConsoleColor.ForegroundGreen);
            Console.WriteLine(FORMAT, "UTC", start.ToString("MM/dd/yyyy HH:mm:ss"), end.ToString("MM/dd/yyyy HH:mm:ss"), string.Format("{0}:{1}:{2}", objTimeSpan.Hours, objTimeSpan.Minutes, objTimeSpan.Seconds));
            Console.WriteLine(FORMAT, "HNS", startPosition.ToString(),              endPosition.ToString(),              (endPosition - startPosition).ToString());
        }

        private static SmoothStreamingManifestParser GetSmoothStreamingManifestParser(string url)
        {
            SmoothStreamingManifestParser objSmoothStreamingManifestParser = null;
            DownloaderManager objDownloaderManager = new DownloaderManager();
            Uri uri = new Uri(url);
            System.IO.Stream objStream = null;
            try
            {
                objStream = objDownloaderManager.DownloadManifest(uri, true);
            }
            catch (Exception e)
            {
                ColorConsole.WriteLine(e.Message, ConsoleColor.ForegroundRed);
            }

            if (objStream != null)
            {
                objSmoothStreamingManifestParser = new SmoothStreamingManifestParser(objStream);
                //SmoothStreamingManifestWriter objSmoothStreamingManifestWriter = new SmoothStreamingManifestWriter(true);
            }
            return objSmoothStreamingManifestParser;
        }

        public static ulong GetStartPositionFromManifest(SmoothStreamingManifestParser objSmoothStreamingManifestParser)
        {
            ulong? maxStartTime = objSmoothStreamingManifestParser.ManifestInfo.Streams.Where(s => s.StreamType.Equals("video", StringComparison.CurrentCultureIgnoreCase)).Max(s => s.Chunks.First().Time);

            if (maxStartTime.HasValue)
            {
                return maxStartTime.Value;
            }

            return 0;
        }

        public static long UtcToHns(DateTime utc)
        {
            DateTime start = GetStartUtc();
            TimeSpan objTimeSpan = (TimeSpan)(utc - start);
            long hns = (long)(objTimeSpan.TotalMilliseconds * 10000);
            return hns;
        }

        public static DateTime HnsToUtc(long hns)
        {
            DateTime start = GetStartUtc();
            DateTime utc = start.AddTicks(hns);
            return utc;
        }

        private static DateTime GetStartUtc()
        {
            return DateTime.Parse("1/1/1970");
        }

        #endregion

        #region Dynamic manifest

        public static void ListManifestFilters(string assetId, CloudMediaContext objCloudMediaContext)
        {
            string FORMAT = "{0,-30}{1,-30}{2,-30}{3,-30}{4,-30}{5,-30}";
            string filtername = string.Empty;
            XmlDocument objXmlDocument;
            ILocator objILocator = GetSasLocator(assetId, objCloudMediaContext);
            string ismfFileName = GetIsmFileName(assetId, objCloudMediaContext);
            ismfFileName += "f";
            string sasUri = GetSasUriForFile(objILocator, ismfFileName);
            objXmlDocument = DownloadToXmlDocument(sasUri);
            FilterManager objFilterManager = new FilterManager();

            ulong start, end;
            string startHns, endHns, startUtc, endUtc;
            filters objfilters;
            ColorConsole.WriteLine(string.Format(FORMAT, "Filter Name", "Filter Text", "Filter Start (HNS)", "Filter Start (UTC)", "Filter End (HNS)", "Filter End (UTC)"), ConsoleColor.ForegroundGreen);
            if (objXmlDocument != null)
            {
                objFilterManager.ReadFilter(objXmlDocument.OuterXml);
                objfilters = objFilterManager.ManfiestFilter;
                if (objfilters != null && objfilters.filter != null && objfilters.filter.Length > 0)
                {
                    foreach (filtersFilter objfiltersFilter in objfilters.filter)
                    {
                        if (objfiltersFilter != null)
                        {
                            start    = objfiltersFilter.absTimeInHNS.geSpecified ? objfiltersFilter.absTimeInHNS.ge : 0;
                            end      = objfiltersFilter.absTimeInHNS.leSpecified ? objfiltersFilter.absTimeInHNS.le : 0;
                            startHns = start > 0 ? start.ToString() : string.Empty;
                            endHns   = end   > 0 ? end.ToString()   : string.Empty;
                            startUtc = start > 0 ? HnsToUtc((long) start).ToString("MM/dd/yyyy HH:mm:ss") : string.Empty;
                            endUtc   = end   > 0 ? HnsToUtc((long) end  ).ToString("MM/dd/yyyy HH:mm:ss") : string.Empty;
                            Console.WriteLine(FORMAT, objfiltersFilter.name, objfiltersFilter.Text, startHns, startUtc, endHns, endUtc);
                            filtername = objfiltersFilter.name;
                        }
                    }

                    //print manifest filter URLs
                    string FORMAT_URL = "{0,-30}{1,-150}";
                    ColorConsole.WriteLine(string.Format(FORMAT_URL, "URL Type", "URL"), ConsoleColor.ForegroundGreen);
                    Console.WriteLine(FORMAT_URL, "SAS for .ismf", sasUri);

                    //print URL with filter name
                    if (!string.IsNullOrEmpty(filtername))
                    {
                        ILocator objILocator_origin = GetOriginLocator(assetId, objCloudMediaContext);
                        string url = GetOriginUri(objILocator_origin);
                        string hss = string.Format("{0}(filtername={1})", url, filtername);
                        string hls = string.Format("{0}(format=m3u8-aapl,filtername={1})", url, filtername);
                        string hds = string.Format("{0}(format=f4m-f4f,filtername={1}).f4m", url, filtername);
                        Console.WriteLine(FORMAT_URL, "Virtual cut (HSS)", hss);
                        Console.WriteLine(FORMAT_URL, "Virtual cut (HLS)", hls);
                        Console.WriteLine(FORMAT_URL, "Virtual cut (HDS)", hds);
                    }
                }
            }
        }

        public static XmlDocument DownloadToXmlDocument(string sasUri)
        {
            XmlDocument objXmlDocument = new XmlDocument();

            CloudBlockBlob objCloudBlockBlob = new CloudBlockBlob(new Uri(sasUri));
            System.IO.Stream objStream = new System.IO.MemoryStream();

            try
            {
                objCloudBlockBlob.DownloadToStream(objStream);
                //Parse it into an XML
                objStream.Position = 0;
                objXmlDocument.Load(objStream);
            }
            catch (Exception e)
            {
                //if there is no .ismf file, SAS URI returns 404
                objXmlDocument = null;
            }

            return objXmlDocument;
        }

        #endregion
    }  //class

    #region Class definitions

    public class MissingFragblob
    {
        public string CloudBobDirectoryPrefix { get; set; }
        public long PreviousFragblob { get; set; }
        public long NextFragblob { get; set; }
    }

    public class TrackInfo
    {
        public string Type { get; set; }
        public string src { get; set; }
        public int systemBitrate { get; set; }
    }

    #endregion
}   //namespace
