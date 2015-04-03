using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.Storage.Blob;
using CreateManifestFilter;
using SmoothStreamingManifestGenerator;


/*
IChannel.IngestUrl  = http://chaventus-nbcprivprod.channel.mediaservices.windows.net/ingest.isml
IChannel.PreviewUrl = http://chaventus-nbcprivprod.channel.mediaservices.windows.net/preview.isml/manifest
 */
namespace PrototypeLive
{
    class Program
    {
        //NBC-private prod
        const string CHANNEL_NAME = "chaventus";
        const string PROGRAM_NAME = "prgaventus05";
        const string ASSET_NAME = "assetaventus05";
        const string ORIGIN_NAME = "default";

        //Teradek POC
        //const string CHANNEL_NAME = "chteradek01";
        //const string PROGRAM_NAME = "prgteradek01";
        //const string ASSET_NAME = "assetteradek01";
        //const string ORIGIN_NAME = "default";

        //const string CHANNEL_NAME = "willzhanChannel10";
        //const string PROGRAM_NAME = "manifest1001";
        //const string ASSET_NAME = "willzhanAsset10";
        //const string ORIGIN_NAME = "origin10";

        //const string CHANNEL_NAME = "nbc01";   //create two programs with same virtual path test
        //const string PROGRAM_NAME = "nbc-sports-live-extra0121112202";
        //const string ASSET_NAME = "nbc01asset";
        //const string ORIGIN_NAME = "nbc01";

        //const string CHANNEL_NAME = "nbc03";   //create two programs with same virtual path test
        //const string PROGRAM_NAME = "nbc03prog";
        //const string ASSET_NAME = "nbc03asset";
        //const string ORIGIN_NAME = "default";

        //const string CHANNEL_NAME = "nbc04";   //create two programs with same virtual path test
        //const string PROGRAM_NAME = "willzhantest01";
        //const string ASSET_NAME = "nbc04asset";
        //const string ORIGIN_NAME = "nbc01";

        //used only in channel/origin batch operations
        static string[] CHANNEL_NAMES = new string[] { 
                                                      "aventus01", "aventus02", "aventus03", "aventus04", "aventus05", "aventus06", "aventus07", "aventus08", 
                                                      "aventus09", "aventus10", "aventus11", "aventus12", "aventus13", "aventus14", "aventus15", "aventus16", 
                                                      //"nbc01", "nbc02", "nbc03", "nbc04", "nbc05", "nbc06", "nbc07", "nbc08", "nbc09", "nbc10", "nbc11", "nbc12",                                             
                                                      //"nbc13", "nbc14", "nbc15", "nbc16", "nbc17", "nbc18", "nbc19", "nbc20",  
                                                      //"nbc24",
                                                      //"mds01", "mds02", "mds03", "mds04", "mds05", "mds06", 
                                                      //"dx01", "dx02", "dx03", "dx04", "dx05", "dx06", "dx07", "dx08", "dx09", "dx10", 
                                                      //"dx11", "dx12", "dx13", "dx14", "dx15", "dx16", "dx17", "dx18", "dx19", "dx20"
                                                     };
        //channel parameters
        //static short FRAGMENTS_PER_SEGMENT = 1;      //NBC Sports: 6sec GOP, 1 fragment/segment
        //static double KEY_FRAME_INTERVAL = 6.0;
        static short FRAGMENTS_PER_SEGMENT = 5;    //2-second key frame interval, 5 fragments/segment
        static double KEY_FRAME_INTERVAL = 2.0;

        static CloudMediaContext objCloudMediaContext;
        static TestStream objTestStream;
        static AcsHelper objAcsHelper;

        static void Main(string[] args)
        {
            //objCloudMediaContext = GetCloudMediaContext();
            AcsInstance objAcsInstance;
            string mediaServiceName, mediaServiceKey;
            //objAcsInstance = AcsInstance.Longevity;
            //mediaServiceName = "isp";
            //objAcsInstance = AcsInstance.Primary;
            //mediaServiceName = "nbcsgliveprodeastms";
            //objAcsInstance = AcsInstance.Replica;
            //mediaServiceName = "nbcsgliveprodwestms";
            //objAcsInstance = AcsInstance.Primary;
            //mediaServiceName = "nbcsgvodprodeastms";
            //objAcsInstance = AcsInstance.Primary;
            //mediaServiceName = "nbcsgvodprodwestms";
            //objAcsInstance = AcsInstance.Primary;
            //mediaServiceName = "nbcsglivetesteastms";
            //objAcsInstance = AcsInstance.Replica;
            //mediaServiceName = "nhkmssoutheastasiaprd";
            //objAcsInstance = AcsInstance.Replica;
            //mediaServiceName = "livedemoamsterdam";
            //objAcsInstance = AcsInstance.Replica;
            //mediaServiceName = "movilmseastusprd";
            //objAcsInstance = AcsInstance.Replica;
            //mediaServiceName = "movilmswestusprd";
            //objAcsInstance = AcsInstance.Replica;
            //mediaServiceName = "nbcsglivetestweuropems";
            //objAcsInstance = AcsInstance.Replica;
            //mediaServiceName = "willzhanmediaservice";
            //objAcsInstance = AcsInstance.Staging;
            //mediaServiceName = "amsstgnbctest";
            objAcsInstance = AcsInstance.Replica;
            mediaServiceName = "willzhanmediaservice2";         //private production for NBC Sports solution test
            //objAcsInstance = AcsInstance.Replica;
            //mediaServiceName = "partnermedia1";
            mediaServiceKey = System.Configuration.ConfigurationManager.AppSettings[mediaServiceName];
            objAcsHelper = new AcsHelper(objAcsInstance, mediaServiceName, mediaServiceKey);
            objCloudMediaContext = objAcsHelper.GetCloudMediaContext();

            IChannel objIChannel;
            IProgram objIProgram;
            ILocator objILocator;
            IAsset objIAsset;
            string assetId; 
            
            //select different case to run.
            int id = 1;

            switch (id)
            {
                case 0:  //CreateChannel
                    CreateChannel(CHANNEL_NAME, false);
                    GetChannel(CHANNEL_NAME);
                    break;
                case 101:  //check/clean programs in a channel
                    objIChannel = GetChannel(CHANNEL_NAME);
                    //objIChannel.Reset();
                    Console.WriteLine("Programs in channel {0}, ID={1}, LastModified = {2}", objIChannel.Name, objIChannel.Id, objIChannel.LastModified.ToString());
                    foreach (IProgram prog in objIChannel.Programs)
                    {
                        Console.WriteLine("\tProgram = {0}, \t\tStatus = {1}, \t\tID = {2}, \t\tAssetId = {3}", prog.Name, prog.State.ToString(), prog.Id, prog.AssetId);
                        //clean up
                        if (prog.State != ProgramState.Stopped)
                        {
                            prog.Stop();
                        }
                        if (prog.State == ProgramState.Stopped)
                        {
                            prog.Delete();
                        }
                    }
                    break;
                case 100: //start channel before using
                    //delete a program
                    //StopProgram(PROGRAM_NAME);
                    //DeleteProgram(PROGRAM_NAME);
                    Console.WriteLine("Starting/stopping/deleting/resetting channel ...");
                    objIChannel = GetChannel(CHANNEL_NAME);
                    objIChannel.Delete();
                    //objIChannel.SendStopOperation();
                    //objIChannel.SendStartOperation();
                    //objIChannel.SendResetOperation();
                    //UpdateGopSettings(objIChannel);
                    Console.WriteLine(string.Format("IChannel.State = {0}, IChannel.LastModified = {1}", objIChannel.State.ToString(), objIChannel.LastModified.ToLongDateString()));
                    break;
                case 102:
                    var assets = objCloudMediaContext.Assets.Where(a => a.Name == ASSET_NAME);
                    foreach (IAsset asset in assets)
                    {
                        Console.WriteLine("AssetID = {0}, LocatorID = {1}, LastModified = {2}", asset.Id, asset.Locators[0].Id, asset.LastModified.ToString("MM-dd-yyyy"));
                    }
                    break;
                case 103:  //create/update/start/stop/delete a LIST of channels, origins
                    //CreateChannels(false);
                    //CreateOrigins(false);
                    //StartChannels();
                    //StopChannels();
                    //StartOrigins();
                    //StopOrigins();
                    //DeleteChannels();
                    //DeleteOrigins();
                    //WamsOperations.ResetAllChannels(objCloudMediaContext);
                    WamsOperations.ListAllGopSettings(objCloudMediaContext);
                    //WamsOperations.UpdateAllOriginSecuritySettings(objCloudMediaContext);    //Include all
                    //WamsOperations.UpdateAllChannelSecuritySettings(objCloudMediaContext);
                    //WamsOperations.ListAllChannelOriginSecuritySettings(objCloudMediaContext);
                    //WamsOperations.MapMediaServices(); //both data centers
                    //WamsOperations.ListAllAssets(objCloudMediaContext);
                    break;
                case 104:  //list all channels and warn on running program
                    WamsOperations.MediaServiceSnapshot(objCloudMediaContext);
                    //ILocator locator = GetSasLocator("nb:cid:UUID:74e32c07-dad2-4243-b318-423de23719b7", objCloudMediaContext);
                    //Console.WriteLine(GetSasUriForFile(locator, "nbc-sports-live-extra1231074104.ism"));
                    break;
                case 105: //get all program origin URLs for a channel
                    GetOriginUrls4AllProgramsInChannel(CHANNEL_NAME);
                    break;
                case 106: //Find asset, East
                    WamsOperations.FindAssetByAssetFileName("nbc-sports-live-extra0117183022.ism", objCloudMediaContext);
                    WamsOperations.FindAssetByAssetName("nbc-sports-live-extra0117183022724-10025", objCloudMediaContext);
                    break;
                case 1:  //CreateProgram and print out URLs: 2 data centers with same virtual paths
                    objTestStream = new TestStream();
                    bool createRedundantProgram = false;  //whether to create redundant program in 2nd data center, need to comment/uncomment in app.config
                    CreatePrograms(CHANNEL_NAME, PROGRAM_NAME, ASSET_NAME, true, PROGRAM_NAME, createRedundantProgram);
                    //StartProgram(PROGRAM_NAME, objCloudMediaContext);
                    //StartProgram(PROGRAM_NAME, GetRedundantCloudMediaContext());

                    PrintHtml();
                    break;
                case 2:  //StartProgram
                    StartProgram(PROGRAM_NAME, objCloudMediaContext);
                    //StartProgram(PROGRAM_NAME, GetRedundantCloudMediaContext());
                    break;
                case 3:  //StopProgram
                    StopProgram(PROGRAM_NAME, objCloudMediaContext);
                    //StopProgram(PROGRAM_NAME, GetRedundantCloudMediaContext());
                    break;
                case 4:  //CreateOrigin
                    CreateOrigin(ORIGIN_NAME);
                    break;
                case 5:  //StartOrigin
                    StartOrigin(ORIGIN_NAME);
                    break;
                case 6:  //StopOrigin
                    StopOrigin(ORIGIN_NAME);
                    break;
                case 7:  //DeleteProgram
                    DeleteProgram(PROGRAM_NAME);
                    break;
                case 8:  //GetProgram
                    //objIProgram = GetProgram(PROGRAM_NAME);
                    objIProgram = GetProgramById("nb:pgid:UUID:c5e2458a-ca5f-442c-a955-a71e1dfc7a7c");
                    break;
                case 9:
                    ProvisionOrigin();
                    break;
                case 10:
                    CheckOrigin();
                    break;
                case 11:  //SAS URL
                    assetId = "nb:cid:UUID:3179bde1-7834-4739-9c0b-7ab7639b8f3f";
                    objILocator = GetSasLocator(assetId, objCloudMediaContext);
                    break;
                case 12:
                    OriginLocatorToSasLocator("287e9820-9c3f-4a34-812b-eb5ebef521fb");
                    break;
                case 13:  //media service storages
                    TestStorageRestApi();
                    break;
                case 14:  //daily operations: stop and start programs in channel 4 and 5
                    StopStartProgramsInChannel4And5();
                    break;
                case 15:  //check origin
                    //IOrigin origin = objCloudMediaContext.Origins.Where(o => o.Name == "default").FirstOrDefault();
                    IStreamingEndpoint origin = GetOrigin(ORIGIN_NAME);
                    Console.WriteLine("RU ={0}, State={1}", origin.ScaleUnits.Value.ToString(), origin.State.ToString());
                    break;
                case 16:  //GOP settings
                    objIChannel = GetChannel(CHANNEL_NAME);
                    //WamsOperations.GetGopSettings(objIChannel);
                    //WamsOperations.UpdateGopSettings(objIChannel, objCloudMediaContext);
                    //WamsOperations.UpdateGopSettingsForAllChannels(objCloudMediaContext);
                    break;
                case 17:  //Sas locator and download XML
                    //TEST 1: in PROD
                    //To test a manifest filter: http://nbc01.nbcsgliveprodeastms.origin.mediaservices.windows.net/1363c000-57ff-48ff-90bf-de7ec75c319a/nbc-sports-live-extra1202142843.ism/manifest(filtername=testfilter02)
                    //.ismf file SAS URI:        https://nbcsgliveprodeaststor.blob.core.windows.net/asset-970eb4a8-f238-432e-8e6d-f3bffd386afa/nbc-sports-live-extra1202142843.ismf?sv=2012-02-12&se=2014-03-14T16%3A47%3A08Z&sr=c&si=7a89a471-0540-49ec-a0e0-f57cc78d59c6&sig=AxqpRvDhZ%2BoSjMmy9i2MunDdO9KEgGVYQIaLulSk37Q%3D
                    //.ism  file SAS URI:        https://nbcsgliveprodeaststor.blob.core.windows.net/asset-970eb4a8-f238-432e-8e6d-f3bffd386afa/nbc-sports-live-extra1202142843.ism?sv=2012-02-12&se=2014-03-14T16%3A47%3A08Z&sr=c&si=7a89a471-0540-49ec-a0e0-f57cc78d59c6&sig=AxqpRvDhZ%2BoSjMmy9i2MunDdO9KEgGVYQIaLulSk37Q%3D
                    //SAS locator path:          https://nbcsgliveprodeaststor.blob.core.windows.net/asset-970eb4a8-f238-432e-8e6d-f3bffd386afa?sv=2012-02-12&se=2014-03-14T16%3A47%3A08Z&sr=c&si=7a89a471-0540-49ec-a0e0-f57cc78d59c6&sig=AxqpRvDhZ%2BoSjMmy9i2MunDdO9KEgGVYQIaLulSk37Q%3D
                    //AddUpdateManifestFilter("nb:cid:UUID:970eb4a8-f238-432e-8e6d-f3bffd386afa", objCloudMediaContext, "testfilter03", 13860330384020841, 13860431894020841);
                    //TEST 2: in Longevity
                    //To test a manifest filter: http://isp.origin.media-test.windows-int.net/b6c9e9d2-b2c2-4c85-9a54-a8b5425e1c36/d607f438-5fab-4a67-a755-b438b32567d0.ism/manifest(filtername=testfilter01)
                    //.ismf file SAS URI:        https://originlongevitytestusb.blob.core.windows.net/asset-4948f9db-338c-45cf-a302-0e9c35c42baf/d607f438-5fab-4a67-a755-b438b32567d0.ismf?sv=2012-02-12&se=2014-03-15T03%3A40%3A47Z&sr=c&si=8701377f-a837-41f4-a722-fb9f704d63d2&sig=GzKGjmF9jFL2x5eB9BM58%2FZje29ed3GeaORlhPFnci4%3D
                    //SAS locator path:          https://originlongevitytestusb.blob.core.windows.net/asset-4948f9db-338c-45cf-a302-0e9c35c42baf?sv=2012-02-12&se=2014-03-15T03%3A40%3A47Z&sr=c&si=8701377f-a837-41f4-a722-fb9f704d63d2&sig=GzKGjmF9jFL2x5eB9BM58%2FZje29ed3GeaORlhPFnci4%3D
                    //string url = "http://isp.origin.media-test.windows-int.net/b6c9e9d2-b2c2-4c85-9a54-a8b5425e1c36/d607f438-5fab-4a67-a755-b438b32567d0.ism/manifest";
                    //IAsset objIAsset = GetAssetFromOriginUrl(url);
                    //AddUpdateManifestFilter(objIAsset.Id, objCloudMediaContext, "testfilter03", 13861418500000000, 13861461000000000);
                    //TEST 3: in Longevity
                    //To test a manifest filter: http://isp.origin.media-test.windows-int.net/4b8782cd-d898-4eb9-9e9a-d1d0fb5f2432/c29e02ec-0197-4846-9c19-c49cf7aacf00.ism/manifest(filtername=ztestfilter01)
                    //.ismf file SAS URI:        https://originlongevitytestusb.blob.core.windows.net/asset-7cdbaf72-0e40-4b3e-9755-98d831a4ec25/c29e02ec-0197-4846-9c19-c49cf7aacf00.ismf?sv=2012-02-12&se=2014-03-14T02%3A51%3A52Z&sr=c&si=63d955cb-606b-4026-9262-dbb62b40931b&sig=u4384d78k2zQKrWm6vyM28GXkRE8DqpH7ZNst4qxqD8%3D      
                    //string url = "http://isp.origin.media-test.windows-int.net/4b8782cd-d898-4eb9-9e9a-d1d0fb5f2432/c29e02ec-0197-4846-9c19-c49cf7aacf00.ism/manifest";
                    //IAsset objIAsset = GetAssetFromOriginUrl(url);
                    //AddUpdateManifestFilter(objIAsset.Id, objCloudMediaContext, "ztestfilter05", 13860036000000000, 13860072000000000);
                    //TEST 4: in Longevity 
                    //string url = "http://isp.origin.media-test.windows-int.net/4b8782cd-d898-4eb9-9e9a-d1d0fb5f2432/c29e02ec-0197-4846-9c19-c49cf7aacf00.ism/manifest";
                    //bool isClipFilterValid = IsClipFilterValid(url, 13860036000000000, 13860072000000000);
                    //Console.WriteLine("IsClipFilterValid = {0}", isClipFilterValid.ToString());
                    //TEST 5: in PROD, CDN edge URL
                    //Test manifest filter: http://olystreameast.nbcolympics.com/nbc01/12b040ef-659d-4ef9-9917-0d0e3071e794/pftnov132013.ism/manifest(filtername=ztestfilter01)
                    //.ismf SAS URI = https://nbcsgliveprodeaststor.blob.core.windows.net/asset-b2bd402b-6b23-461d-a9a4-6903e834e3a1/pftnov132013.ismf?sv=2012-02-12&se=2014-03-16T20%3A22%3A33Z&sr=c&si=cc66066b-f121-4b5b-960e-6377540ba96b&sig=55FDaQtGB1hUk6xNyOuzGVxNuVq6eDBrtGlUhC2Lbtg%3D
                    //Streaming asset Id: nb:cid:UUID:b2bd402b-6b23-461d-a9a4-6903e834e3a1
                    //384544:30:00       HNS: 13843602000000000
                    //384545:30:00       HNS: 13843638000000000 
                    //string url = "http://olystreameast.nbcolympics.com/nbc01/12b040ef-659d-4ef9-9917-0d0e3071e794/pftnov132013.ism/manifest";
                    //IAsset objIAsset = GetAssetFromOriginUrl(url);
                    ////ulong? clipBegin = 13843602000000000;
                    ////ulong? clipEnd = 13843602000000000;
                    ////ulong? clipBegin = null;
                    ////ulong? clipEnd = 13843602000000000;
                    //ulong? clipBegin = 13843602000000000;
                    //ulong? clipEnd = null;
                    //bool isClipFilterValid = IsClipFilterValid(url, clipBegin, clipEnd);
                    //Console.WriteLine("IsClipFilterValid = {0}", isClipFilterValid.ToString());
                    //AddUpdateManifestFilter(objIAsset.Id, objCloudMediaContext, "ztestfilter05", clipBegin, clipEnd);
                    //TEST 6
                    //GetOriginLocator("nb:cid:UUID:ed44ec96-eb1d-4ba5-9e61-5aabf846b4f6", objCloudMediaContext);
                    //string xml = System.IO.File.ReadAllText("filter.xml");
                    //RemoveNamespace(xml);
                    //Test();
                    AddUpdateManifestFilter("nb:cid:UUID:231d66dc-851f-4ca3-b3ae-98bd2b70a000", objCloudMediaContext, "vodcut2", 13909334282989666, null);
                    break;
                case 18: //what can an URL provide?
                    string uri;
                    //uri = "http://isp.origin.media-test.windows-int.net/9fc9a0a9-a320-4c3f-b5ea-11472c9e8ba5/AudioRedundancyCheck01.ism/manifest";
                    //uri = "http://olystreameast.nbcolympics.com/vod/35596506-0c07-49f6-ab78-9c4ef49e34d3/alpine-skiing0111083506.ism/manifest"; //with 2 time line markers
                    //uri = "http://nbc01.nbcsgliveprodeastms.origin.mediaservices.windows.net/1363c000-57ff-48ff-90bf-de7ec75c319a/nbc-sports-live-extra1202142843.ism/manifest(filtername=testfilter02)";
                    //uri = "http://isp.origin.media-test.windows-int.net/3f0afe84-f559-494e-bd36-46e9a1c179e3/4e330fb6-ebb1-4dae-98d9-282e2f298c90.ism/manifest";  //48 hours duration
                    //uri = "http://isp.origin.media-test.windows-int.net/4b8782cd-d898-4eb9-9e9a-d1d0fb5f2432/c29e02ec-0197-4846-9c19-c49cf7aacf00.ism/manifest";
                    //uri = "http://olyvodeast.nbcolympics.com/vod/de0d8d6b-558a-4048-a1af-303eb5c25218/RMP_Test2ElementalEmbeddedCaptions_Avid_2MINCC.ism/manifest";
                    //uri = "nb:cid:UUID:2afd3dd9-5ed4-4367-9d3f-89632e99845f";
                    //uri = "nb:cid:UUID:7ab04c1f-72b7-4151-ad2d-1802b4b23989";
                    //uri = "http://nbcsgvodprodeastms.origin.mediaservices.windows.net/90335a5d-6525-4d9a-b98b-422aafae6813/disney_princeofpersia.ism/Manifest";
                    //uri = "http://nbc01.nbcsgliveprodeastms.origin.mediaservices.windows.net/a8abfdc7-b181-4eb6-a5b5-ba5d382a0288/nbc-sports-live-extra0117075520.ism/manifest";
                    //uri = "http://nbcsgliveprodeastms.origin.mediaservices.windows.net/6f2862b3-2274-4f8c-82f9-5c52f34744aa/90_PB_PIRELLI_ROUND3_130414.ism/Manifest";
                    //uri = "http://nbcsgliveprodeastms.origin.mediaservices.windows.net/9fc03898-3d5b-4cd2-9f09-de477b2023d6/nbc-sports-live-extra0117144535.ism/manifest";
                    //uri = "nb:cid:UUID:6f3fb58c-0f3a-4ee6-aa87-cd74be62a8b7";   //PID=10025
                    //uri = "http://nbc09.nbcsgliveprodeastms.origin.mediaservices.windows.net/2f3525c6-2e8e-4616-af19-200d9b4bf5f5/nbc-sports-live-extra0118095233.ism/manifest";
                    //uri = "http://nbc10.nbcsgliveprodeastms.origin.mediaservices.windows.net/77347f82-9c7d-494c-b71d-0803734898b8/nbc-sports-live-extra0118095239.ism/manifest";
                    //uri = "http://nbc01.nbcsgliveprodeastms.origin.mediaservices.windows.net/b85f3aaf-3ec9-4fd2-894c-58cba1ad1e9e/nbc-sports-live-extra0119080415-ua.ism/manifest";
                    //uri = "http://olystreamstag.nbcolympics.com/31a3387e-3df7-4e0d-a633-b33c867fae9f/AdobePlayerTest01.ism/manifest";
                    //uri = "http://nbc01.nbcsgliveprodeastms.origin.mediaservices.windows.net/7d59ae6d-024c-414b-92b6-9391bc5bd7a8/nbc-sports-live-extra0121121946.ism/manifest";
                    //uri = "http://nbc01.nbcsgliveprodwestms.origin.mediaservices.windows.net/445b03ac-516d-4f29-b6bd-2daabc10548a/test0121142643-ua.ism/manifest";  //IBC distribution center
                    //uri = "http://olystreameast.nbcolympics.com/vod/ec5201fa-4ffa-4f74-9b3e-53dfdb6ae78f/bhaveshfertestnbc110121165454-ua.ism/manifest";   //has manifest filter, missing last fragments at end
                    //uri = "http://olyvodeast.nbcolympics.com/vod/557f6b79-e7fd-4b6d-91c1-b0911cb16ea4/oly14_as_Mancuso_bcast_profile_140120.ism/manifest";   //has timeline marker
                    //uri = "http://isp.origin.media-test.windows-int.net/94f258a5-0112-4323-82a4-544a54be2b3d/Gen2LiveMidRollsPlayerTest16.ism/manifest";
                    //uri = "http://nbcsgliveprodeastms.origin.mediaservices.windows.net/ab657d8d-fd8c-493e-b506-d6e94e34f453/nbc-sports-live-extra0123075221-ua.ism/manifest";
                    //uri = "http://nbc01.nbcsgliveprodeastms.origin.mediaservices.windows.net/04e2554d-8076-454f-861d-a5957d959791/nbc-sports-live-extra0122140544.ism/manifest";
                    //uri = "http://nbc24.nbcsgliveprodeastms.origin.mediaservices.windows.net/6d153605-6120-4307-a71a-95e8a9ea76fb/nbc-sports-live-extra0122074430-ua.ism/manifest";
                    //uri = "http://nbc01.nbcsgliveprodeastms.origin.mediaservices.windows.net/4861412d-6be3-4529-b458-b7a31257b55a/nbc-sports-live-extra0124060038.ism/manifest";  //has quite some gaps
                    //uri = "http://nbcsgliveprodeastms.origin.mediaservices.windows.net/8efc6002-9d89-4c0f-a6b0-197a285c4cf7/ibc-sochi-nbc-test.ism/manifest";
                    //uri = "http://nbcsgliveprodwestms.origin.mediaservices.windows.net/8efc6002-9d89-4c0f-a6b0-197a285c4cf7/ibc-sochi-nbc-test.ism/manifest";
                    //uri = "http://isp.origin.media-test.windows-int.net/d934f990-d151-4f85-885e-a8ea3e837423/Gen2LiveMidRollsPlayerTest14.ism/manifest(format=m3u8-aapl)";  //9 hours, has holes inside
                    //uri = "http://nbc01.nbcsgliveprodeastms.origin.mediaservices.windows.net/c1e86efb-f626-4e8a-8f7a-a4ae0d71a97c/nbc-sports-live-extra0126135304-ua.ism/manifest";
                    //uri = "http://isp.origin.media-test.windows-int.net/290edfac-5d86-4b37-b04b-0254241f2340/Gen2LiveMidRollsPlayerTest24.ism/manifest(format=m3u8-aapl)";
                    //uri = "http://olystreamwest.nbcolympics.com/nbc02/015e66b3-4e12-40fc-adab-155f21639728/nbc-sports-live-extra0128095200.ism/manifest(format=f4m-f4f).f4m";
                    //uri = "http://olystreameast.nbcolympics.com/nbc07/0ed89405-6b8e-4149-8877-e189f4abc2ff/nbc-sports-live-extra0128101452.ism/manifest";
                    //uri = "nb:cid:UUID:1ab28547-d7a6-4b57-a15e-15ffe026d326";
                    //uri = "http://olystreamwest.nbcolympics.com/vod/9fe1e950-c353-42d3-bf84-9fe5b02b02be/nbc-sports-live-extra0128072732.ism/manifest";
                    //uri = "nb:cid:UUID:d647d385-9c83-464d-a420-04dcda53ce0c";
                    //uri = "http://mds02.nbcsgliveprodwestms.origin.mediaservices.windows.net/bdb2d915-e87f-43fc-ab33-3a282759a267/women-s-moguls--qualifying0206050107.ism/manifest";//long FragmentWaitTime
                    //uri = "http://dx12.nbcsgliveprodwestms.origin.mediaservices.windows.net/dc1501be-39c0-4fbc-8e72-565aeaddde2e/w-rus-den-curling02100058397418.ism/manifest"; //both 27-32 seconds delay in fragblob writes and missing fragblobs
                    //uri = "http://dx08.nbcsgliveprodwestms.origin.mediaservices.windows.net/caa2a294-67b2-4906-8519-c5e568763097/usa-sui-w-hockey02100058397403.ism/manifest";  //both 27-32 seconds delay in fragblob writes and missing fragblobs
                    //uri = "http://dx11.nbcsgliveprodeastms.origin.mediaservices.windows.net/4d41f456-94b1-48ac-ade5-53ec215e34d2/w-swe-gbr-curling02100058397412.ism/manifest";   //both 27-32 seconds delay in fragblob writes and missing fragblobs
                    //uri = "http://olystreamwest.nbcolympics.com/vod/5460f7e9-572f-470b-bc35-bbb93d9aea04/midday-session--women-s-prelims--sheet-b--sui---us.ism/manifest";
                    //prevous program MDS02-05
                    //uri = "http://mds02.nbcsgliveprodeastms.origin.mediaservices.windows.net/c1df21ce-dffa-4fda-9d66-b74660d6bb83/replay-figure-skating-m-short-prog-day-602131958509036.ism/manifest";
                    //uri = "http://mds03.nbcsgliveprodeastms.origin.mediaservices.windows.net/42f37cb6-9616-4cbc-96ca-dca9d0a6aa0b/am-curling--swe-chn0213185758.ism/manifest";
                    //uri = "http://mds04.nbcsgliveprodeastms.origin.mediaservices.windows.net/8d30d945-6d16-4881-8daa-2afdbf935464/amsession--men-s-prelims--sheet-c--usa---ger021319.ism/manifest";
                    //uri = "http://mds05.nbcsgliveprodeastms.origin.mediaservices.windows.net/904a79b9-cb46-4184-bd2e-9daa260363f4/amsession-men-s-prelims--sheet-d--can---nor0213191.ism/manifest";
                    //uri = "http://mds06.nbcsgliveprodeastms.origin.mediaservices.windows.net/09357f6e-4d70-4150-9499-deb0191faad3/replay-bt-m-20km-indv--day-602131958509048.ism/manifest";
                    //new program MDS03-05
                    //uri = "http://mds03.nbcsgliveprodeastms.origin.mediaservices.windows.net/ec4b9852-75b1-4c7f-b0dc-671c7fcfdabf/am-curling--swe-chn0213210239.ism/manifest";
                    //uri = "http://mds04.nbcsgliveprodeastms.origin.mediaservices.windows.net/8176008e-5ce0-4467-87dc-a6638081cef4/amsession--men-s-prelims--sheet-c--usa---ger021321.ism/manifest";
                    //uri = "http://mds05.nbcsgliveprodeastms.origin.mediaservices.windows.net/e6e26db1-cc2b-4937-ac66-84eb5955111d/amsession-men-s-prelims--sheet-d--can---nor0213211.ism/manifest";
                    //uri = "http://nbc01.nbcsgliveprodeastms.origin.mediaservices.windows.net/c30a23b8-510c-4a3c-97fe-703ff5746576/olympics-2014-prime-time-show0214161425-ua.ism/manifest";
                    //uri = "http://nbc01.nbcsgliveprodeastms.origin.mediaservices.windows.net/bfc40523-edda-43b5-a5a1-7a90ba5eae0d/olympics-2014-prime-time-show0215162229-ua.ism/manifest";   //archive delay check
                    //uri = "http://mds04.nbcsgliveprodeastms.origin.mediaservices.windows.net/6133472c-bd82-45bb-bcc4-a0830a3d6e3a/amcurling--gbr-nor0215193312.ism/manifest";
                    //uri = "http://dx09.nbcsgliveprodeastms.origin.mediaservices.windows.net/d7aaec3e-5d7a-4aa8-832d-58ec17f00f19/m-usa-can-curling02151958396367.ism/manifest";
                    //uri = "http://mds06.nbcsgliveprodeastms.origin.mediaservices.windows.net/b47edea0-52d4-407a-ae8a-4d0cf54a57a7/midday-session--men-s-prelims-nor---den0217001810.ism/manifest";
                    //uri = "http://nbcsgliveprodeastms.origin.mediaservices.windows.net/82521075-8c29-4c4f-9857-221d0285de63/nbcsn--day-14-coverage0220222034.ism/manifest";
                    //uri = "http://nbcsgliveprodeastms.origin.mediaservices.windows.net/2f6516c1-6287-4043-b384-8fd932a92bd9/6ca689a4-55bf-45bc-9a77-32d11f5577e9.ism/manifest";
                    //uri = "http://isp.origin.media-test.windows-int.net/056adac4-21b2-4e48-88d0-2be4c78cb3bd/audiotest.ism/manifest";
                    //uri = "http://willzhanmediaservice2.streaming.mediaservices.windows.net/ad950889-c9c1-47ac-93d2-31874f17c2b9/prgaventus02.ism/Manifest";
                    uri = "http://willzhanmediaservice2.streaming.mediaservices.windows.net/276714cc-05f5-48aa-8916-c743bb80b23d/prgaventus01.ism/Manifest";
                    WamsOperations.GetInfoFromUrl(uri, objCloudMediaContext);
                    break;
                case 181:  //find asset from PID
                    objIAsset = WamsOperations.FindAssetByPid(15455, objCloudMediaContext);
                    if (objIAsset != null) WamsOperations.GetInfoFromUrl(objIAsset.Id, objCloudMediaContext);
                    break;
                default:
                    break;
            }

            Console.WriteLine("Hit any key to exit.");
            Console.ReadKey();
        }

        #region CloudMediaContext

        public static CloudMediaContext GetRedundantCloudMediaContext()
        {
            string mediaServiceName = "nbcsgliveprodwestms";
            string mediaServiceKey = System.Configuration.ConfigurationManager.AppSettings[mediaServiceName];
            AcsHelper objAcsHelper = new AcsHelper(AcsInstance.Replica, mediaServiceName, mediaServiceKey);
            CloudMediaContext objCloudMediaContextRedundant = objAcsHelper.GetCloudMediaContext();
            Console.WriteLine("Redundant CloudMediaContext created");

            return objCloudMediaContextRedundant;
        }

        public static void Test()
        {
            CloudMediaContext context = null;
            AcsHelper objAcsHelper;
            AcsInstance objAcsInstance = AcsInstance.Replica;
            string mediaServiceName    = "nbcsgliveprodwestms";
            string mediaServiceKey     = System.Configuration.ConfigurationManager.AppSettings[mediaServiceName];
            Logger.Log("Starting the CloudMediaContext test", System.Diagnostics.EventLogEntryType.Information);
            while (true)
            {
                //objTestStream = new TestStream();
                //CreatePrograms(CHANNEL_NAME, PROGRAM_NAME, ASSET_NAME, true, PROGRAM_NAME, false);
                //DeleteProgram(PROGRAM_NAME);
                //Console.WriteLine("Program created and deleted/");

                objAcsHelper = new AcsHelper(objAcsInstance, mediaServiceName, mediaServiceKey);
                try
                {
                    context = objAcsHelper.GetCloudMediaContext();
                }
                catch (Exception e)
                {
                    ColorConsole.WriteLine(e.Message + e.StackTrace, ConsoleColor.ForegroundRed);
                    Logger.Log(e.Message, System.Diagnostics.EventLogEntryType.Error);
                }
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(1.0));
                //Console.WriteLine("Channel count = " + context.Channels.Count().ToString());
            }
        }
        #endregion

        #region Channel
        //http://msdn.microsoft.com/en-us/library/azure/dn783465.aspx 

        public static List<IPRange> CreateIPRangeList()
        {
            //CIDR address ranges are allowed.  In the address IPv4 property we support v.v.v.v , x.x.x.x (y.y.y.y) , and w.w.w.w/z.
            //IP to CIDR converter: http://ip2cidr.com/
            string[] whitelistGroups = new string[] {     "IPv4AllowList_Open"//,
                                                          //"IPv4AllowList_Aventus",
                                                          //"IPv4AllowList_iStreamPlanet",
                                                          //"IPv4AllowList_WAMS",
                                                          //"IPv4AllowList_Charlotte",
                                                          //"IPv4AllowList_Akamai_SiteShield",
                                                          //"IPv4AllowList_Akamai_Support",
                                                          //"IPv4AllowList_Akamai_Staging",
                                                          //"IPv4AllowList_SnappyTV",
                                                          //"IPv4AllowList_Southworks",
                                                          //"IPv4AllowList_Misc" 
                                                    };
            Tuple<string, string>[] objTuples = new Tuple<string, string>[whitelistGroups.Length];
            for (int i = 0; i < whitelistGroups.Length; i++)
            {
                objTuples[i] = new Tuple<string, string>(whitelistGroups[i], System.Configuration.ConfigurationManager.AppSettings[whitelistGroups[i]]);
            }

            List<IPRange> objList_IPRange = new List<IPRange>();
            string ipGroupName, ipGroup, ip;
            int subnetPrefixLength;
            string[] ips;
            for (int i = 0; i < objTuples.Length; i++)
            {
                ipGroupName = objTuples[i].Item1.ToLower();
                ipGroup = objTuples[i].Item2;
                ips = ipGroup.Split(new char[] { ';' });
                for (int j = 0; j < ips.Length; j++)
                {
                    if (ips[j].Contains("/"))
                    {
                        ip = ips[j].Split(new char[] { '/' })[0];
                        subnetPrefixLength = int.Parse(ips[j].Split(new char[] { '/' })[1]);
                    }
                    else
                    {
                        ip = ips[j];
                        subnetPrefixLength = 0;
                    }
                    objList_IPRange.Add(new IPRange
                        {
                            Name = string.Format("{0}_{1}", ipGroupName, j.ToString()),
                            Address = System.Net.IPAddress.Parse(ip),
                            SubnetPrefixLength = subnetPrefixLength
                        });
                }
            }

            return objList_IPRange;
        }
        private static ChannelInput CreateChannelInput()
        {
            return new ChannelInput
            {
                KeyFrameInterval  = TimeSpan.FromSeconds(KEY_FRAME_INTERVAL),     
                StreamingProtocol = StreamingProtocol.RTMP,//FragmentedMP4,
                AccessControl = new ChannelAccessControl
                {
                    IPAllowList = CreateIPRangeList()
                }
            };
        }

        private static ChannelPreview CreateChannelPreview()
        {
            return new ChannelPreview
            {
                AccessControl = new ChannelAccessControl
                {
                    IPAllowList = CreateIPRangeList()
                }
            };
        }

        private static ChannelOutput CreateChannelOutput()
        {
            return new ChannelOutput
            {
                Hls = new ChannelOutputHls { FragmentsPerSegment = FRAGMENTS_PER_SEGMENT} 
            };
        }

        public static IChannel CreateChannel(string channelName, bool startChannel)
        {
            int count = objCloudMediaContext.Channels.AsEnumerable().Where(c => c.Name == channelName).Count();
            IChannel objIChannel;
            if (count <= 0)
            {
                objIChannel = objCloudMediaContext.Channels.Create(
                                                                    new ChannelCreationOptions
                                                                    {
                                                                         Name    = channelName,
                                                                         Input   = CreateChannelInput(),
                                                                         Preview = CreateChannelPreview(),
                                                                         Output  = CreateChannelOutput()
                                                                    });

                Console.WriteLine(string.Format("Channel {0} created", channelName));
                if (startChannel)
                {
                    objIChannel.Start();
                    Console.WriteLine(string.Format("Channel {0} started", channelName));
                }
            }
            else
            {
                Console.WriteLine(string.Format("Channel {0} already exists", channelName));
                objIChannel = objCloudMediaContext.Channels.AsEnumerable().Where(c => c.Name == channelName).FirstOrDefault();
                //if (objIChannel.State != ChannelState.Stopped) objIChannel.Stop();
                //objIChannel.Delete();
                Console.WriteLine(string.Format("Channel {0} deleted", channelName));
            }

            return objIChannel;
        }

        public static IChannel GetChannel(string channelName)
        {
            IChannel objIChannel = objCloudMediaContext.Channels.Where(c => c.Name == channelName).FirstOrDefault();
            Console.WriteLine(string.Format("IChannel.IngestUrl = {0}", objIChannel.Input.Endpoints.FirstOrDefault().Url.ToString()));
            Console.WriteLine(string.Format("IChannel.PreviewUrl = {0}", objIChannel.Preview.Endpoints.FirstOrDefault().Url.ToString()));
            return objIChannel;
        }
  
        #endregion

        #region Program
        //The following variables can be null or string.Empty if not needed:
        //storageAccountName: if null, default storage is used;
        //locatorIdToReplicate, accountName, accountKey: If locatorIdToReplicate is not null, accountName and accountKey cannot be null.
        //manifestFileName: if null, manifest file name will be generated as a GUID.
        public static ProgramInfo CreateProgramInfo(string channelName, string programName, string programDescription, string assetName, bool enableArchive,
                                                    CloudMediaContext objCloudMediaContext, string accountName, string accountKey,
                                                    string storageAccountName, string manifestFileName, string locatorIdToReplicate)   
        {
            //IAsset
            IAsset objIAsset;
            if (string.IsNullOrEmpty(storageAccountName))
            {
                //use default storage
                objIAsset = objCloudMediaContext.Assets.Create(assetName, AssetCreationOptions.None);
            }
            else
            {
                //use specified storage
                objIAsset = objCloudMediaContext.Assets.Create(assetName, storageAccountName, AssetCreationOptions.None);
            }
            Console.WriteLine("IAsset created");

            //ILocator
            string locatorPath; 
            string locatorId;
            if (string.IsNullOrEmpty(locatorIdToReplicate))
            {
                //ILocator.Id will be generated
                IAccessPolicy objIAccessPolicy = objCloudMediaContext.AccessPolicies.Create("Streaming policy", TimeSpan.FromDays(30), AccessPermissions.Read);
                ILocator objILocator = objCloudMediaContext.Locators.CreateLocator(LocatorType.OnDemandOrigin, objIAsset, objIAccessPolicy, DateTime.UtcNow.AddMinutes(-5));
                locatorPath = objILocator.Path;
                locatorId   = objILocator.Id;
            }
            else
            {
                //ILocator.Id will be the same as specified: locatorIdToReplicate
                locatorPath = CreateOriginLocatorWithRest(objCloudMediaContext, accountName, accountKey, locatorIdToReplicate, objIAsset.Id);
                locatorId   = locatorIdToReplicate;
            }
            Console.WriteLine("ILocator created.");

            //IProgram
            IProgram objIProgram;
            IChannel objIChannel = objCloudMediaContext.Channels.Where(c => c.Name == channelName).FirstOrDefault();
            ProgramCreationOptions options;
            if (string.IsNullOrEmpty(manifestFileName))
            {
                //manifest file name will be a generated GUID
                options = new ProgramCreationOptions(){
                    Name = programName,
                    Description = programDescription,
                    ArchiveWindowLength = TimeSpan.FromMinutes(60.0),
                    AssetId = objIAsset.Id
                };
                objIProgram = objIChannel.Programs.Create(options);
            }
            else
            {
                options = new ProgramCreationOptions()
                {
                    Name = programName,
                    Description = programDescription,
                    ArchiveWindowLength = TimeSpan.FromMinutes(60.0),
                    ManifestName = manifestFileName, //manifest file name to be duplicated (without .ism suffix)
                    AssetId = objIAsset.Id
                };
                //manifest file name will be as specified: manifestFileName
                objIProgram = objIChannel.Programs.Create(options);
            }
            Console.WriteLine("IProgram created");

            //Query the primary ism file to contruct program streaming url
            var theManifest = from f in objIAsset.AssetFiles where f.Name.EndsWith(".ism") && f.IsPrimary select f;
            IAssetFile objIAssetFile = theManifest.First();

            //load objProgramInfo
            ProgramInfo objProgramInfo = new ProgramInfo();
            objProgramInfo.Program          = objIProgram;
            objProgramInfo.Asset            = objIAsset;
            objProgramInfo.LocatorPath      = locatorPath;
            objProgramInfo.LocatorId        = locatorId;
            objProgramInfo.ManifestFileName = objIAssetFile.Name;
               
            return objProgramInfo;
        }

        //Programs created in separate data centers, media services, CloudMediaContexts, channels
        //2 programs with URLs like below
        //http://nbc01.nbcsgliveprodeastms.origin.mediaservices.windows.net/adb7a28a-9360-4dc2-a1f0-96d05c135a2f/virtualpath.ism/Manifest
        //http://nbc01.nbcsgliveprodwestms.origin.mediaservices.windows.net/adb7a28a-9360-4dc2-a1f0-96d05c135a2f/virtualpath.ism/Manifest
        public static void CreatePrograms(string channelName, string programName, string assetName, bool enableArchive, string manifestFileName, bool createRedundantProgram)
        {
            string programDesc = string.Format("Created at {0} GMT by willzhan", DateTime.UtcNow.ToString("MM/dd/yyyy HH:mm"));
            string storageAccountName = System.Configuration.ConfigurationManager.AppSettings["MediaServiceStorageName"];
            ProgramInfo objProgramInfo = CreateProgramInfo(channelName, programName, programDesc, assetName, true, objCloudMediaContext, null, null, storageAccountName, manifestFileName, null);  //locator ID is genereated

            string hss = string.Format("{0}{1}/manifest",                     objProgramInfo.LocatorPath, objProgramInfo.ManifestFileName);
            string hls = string.Format("{0}{1}/manifest(format=m3u8-aapl)",   objProgramInfo.LocatorPath, objProgramInfo.ManifestFileName);
            string hds = string.Format("{0}{1}/manifest(format=f4m-f4f).f4m", objProgramInfo.LocatorPath, objProgramInfo.ManifestFileName);

            //print origin URLs
            Console.WriteLine("Manifest file name = {0}", objProgramInfo.ManifestFileName);
            Console.WriteLine("AssetID = {0}", objProgramInfo.Asset.Id);
            Console.WriteLine("HSS: " + hss);
            Console.WriteLine("HLS: " + hls);
            Console.WriteLine("HDS: " + hds); 

            //load TestStream object for later HTML segment
            objTestStream.OriginHssUrl0 = hss;
            objTestStream.OriginHlsUrl0 = hls;
            objTestStream.OriginHdsUrl0 = hds;

            string edgeHost = System.Configuration.ConfigurationManager.AppSettings["EdgeHost"];
            if (!string.IsNullOrEmpty(edgeHost))
            {
                Console.WriteLine("CDN edge URLs:");
                string path = objProgramInfo.LocatorPath;
                UriBuilder objUriBuilder = new UriBuilder(path);
                path = path.Replace(objUriBuilder.Host, edgeHost + "/" + ORIGIN_NAME);

                hss = string.Format("{0}{1}/manifest",                     path, objProgramInfo.ManifestFileName);
                hls = string.Format("{0}{1}/manifest(format=m3u8-aapl)",   path, objProgramInfo.ManifestFileName);
                hds = string.Format("{0}{1}/manifest(format=f4m-f4f).f4m", path, objProgramInfo.ManifestFileName);

                Console.WriteLine("HSS: " + hss);
                Console.WriteLine("HLS: " + hls);
                Console.WriteLine("HDS: " + hds);

                //load TestStream object for later HTML segment
                objTestStream.EdgeHssUrl0 = hss;
                objTestStream.EdgeHlsUrl0 = hls;
                objTestStream.EdgeHdsUrl0 = hds;
            }

            //create SAS locator
            objTestStream.SasUrl0 = GetSasLocator(objProgramInfo.Asset.Id, objCloudMediaContext).Path;

            //create a program with same virtual path: same ILocator.Path and same IAssetFile.Name
            if (createRedundantProgram)
            {
                string accountName = System.Configuration.ConfigurationManager.AppSettings["RedundantMediaServiceAccountName"];
                string accountKey  = System.Configuration.ConfigurationManager.AppSettings["RedundantMediaServiceAccountKey"];
                string storageName = System.Configuration.ConfigurationManager.AppSettings["RedundantMediaServiceStorageName"];
                CloudMediaContext objCloudMediaContextRedundant = GetRedundantCloudMediaContext();
                objProgramInfo = CreateProgramInfo(channelName, programName, programDesc, assetName, true, objCloudMediaContextRedundant, accountName, accountKey, storageName, manifestFileName, objProgramInfo.LocatorId);

                hss = string.Format("{0}{1}/manifest",                     objProgramInfo.LocatorPath, objProgramInfo.ManifestFileName);
                hls = string.Format("{0}{1}/manifest(format=m3u8-aapl)",   objProgramInfo.LocatorPath, objProgramInfo.ManifestFileName);
                hds = string.Format("{0}{1}/manifest(format=f4m-f4f).f4m", objProgramInfo.LocatorPath, objProgramInfo.ManifestFileName);

                //print origin URLs
                Console.WriteLine("Manifest file name = {0}", objProgramInfo.ManifestFileName);
                Console.WriteLine("AssetID = {0}", objProgramInfo.Asset.Id);
                Console.WriteLine("HSS: " + hss);
                Console.WriteLine("HLS: " + hls);
                Console.WriteLine("HDS: " + hds);

                //load TestStream object for later HTML segment
                objTestStream.OriginHssUrl1 = hss;
                objTestStream.OriginHlsUrl1 = hls;
                objTestStream.OriginHdsUrl1 = hds;

                edgeHost = System.Configuration.ConfigurationManager.AppSettings["RedundantEdgeHost"];
                if (!string.IsNullOrEmpty(edgeHost))
                {
                    Console.WriteLine("CDN edge URLs:");
                    string locatorPath = objProgramInfo.LocatorPath;
                    UriBuilder objUriBuilder = new UriBuilder(locatorPath);
                    locatorPath = locatorPath.Replace(objUriBuilder.Host, edgeHost + "/" + ORIGIN_NAME);

                    hss = string.Format("{0}{1}/manifest",                     locatorPath, objProgramInfo.ManifestFileName);
                    hls = string.Format("{0}{1}/manifest(format=m3u8-aapl)",   locatorPath, objProgramInfo.ManifestFileName);
                    hds = string.Format("{0}{1}/manifest(format=f4m-f4f).f4m", locatorPath, objProgramInfo.ManifestFileName);

                    Console.WriteLine("HSS: " + hss);
                    Console.WriteLine("HLS: " + hls);
                    Console.WriteLine("HDS: " + hds);

                    //load TestStream object for later HTML segment
                    objTestStream.EdgeHssUrl1 = hss;
                    objTestStream.EdgeHlsUrl1 = hls;
                    objTestStream.EdgeHdsUrl1 = hds;

                }

                //create SAS locator
                objTestStream.SasUrl1 = GetSasLocator(objProgramInfo.Asset.Id, objCloudMediaContextRedundant).Path;
            }
        }

        public static void StartProgram(string programName, CloudMediaContext objCloudMediaContext)
        {
            IProgram objIProgram = objCloudMediaContext.Programs.Where(p => p.Name == programName).FirstOrDefault();
            objIProgram.Start();
            Console.WriteLine("Program {0} has started", objIProgram.Name);
        }

        public static IProgram StartProgramAsync(string programName)
        {
            IProgram objIProgram = objCloudMediaContext.Programs.Where(p => p.Name == programName).FirstOrDefault();
            IOperation objIOperation = objIProgram.SendStartOperation();

            var state = objIOperation.State;
            while (state == OperationState.InProgress)
            {
                System.Threading.Thread.Sleep(1000);
                var op = objCloudMediaContext.Operations.GetOperation(objIOperation.Id);
                state = op.State;
                switch (state)
                {
                    case OperationState.Failed:
                        // Handle the failure.
                        break;
                    case OperationState.InProgress:
                        break;
                    case OperationState.Succeeded:
                        break;
                }
            }

            objIProgram = objCloudMediaContext.Programs.Where(p => p.Id == objIOperation.TargetEntityId).Single();

            return objIProgram;
        }

        public static void StopProgram(string programName, CloudMediaContext objCloudMediaContext)
        {
            IProgram objIProgram = objCloudMediaContext.Programs.Where(p => p.Name == programName).FirstOrDefault();
            if (objIProgram.State == ProgramState.Running)
            {
                objIProgram.Stop();
            }
            Console.WriteLine("Program {0} has stopped.", objIProgram.Name);
        }

        public static void DeleteProgram(string programName)
        {
            IProgram program1 = objCloudMediaContext.Programs.Where(p => p.Name == programName).FirstOrDefault();
            program1.Delete();
            Console.WriteLine("Program {0} deleted", program1.Name);
        }

        public static IProgram GetProgram(string programName)
        {
            IProgram objIProgram = objCloudMediaContext.Programs.Where(p => p.Name == programName).FirstOrDefault();
            Console.WriteLine("Program.Id = {0}", objIProgram.Id);
            Console.WriteLine("Program.AssetId = {0}", objIProgram.AssetId);
            Console.WriteLine("Program.ChannelId = {0}", objIProgram.ChannelId);
            IAsset objIAsset = objCloudMediaContext.Assets.Where(a => a.Id == objIProgram.AssetId).FirstOrDefault();
            Console.WriteLine("Asset.Name = {0}", objIAsset.Name);
            Console.WriteLine("Program.State = {0}", objIProgram.State.ToString());
            return objIProgram;
        }

        public static IProgram GetProgramById(string programId)
        {
            IProgram objIProgram = objCloudMediaContext.Programs.Where(p => p.Id == programId).FirstOrDefault();
            Console.WriteLine("Program.Id = {0}", objIProgram.Id);
            Console.WriteLine("Program.AssetId = {0}", objIProgram.AssetId);
            Console.WriteLine("Program.ChannelId = {0}", objIProgram.ChannelId);
            IAsset objIAsset = objCloudMediaContext.Assets.Where(a => a.Id == objIProgram.AssetId).FirstOrDefault();
            Console.WriteLine("Asset.Name = {0}", objIAsset.Name);
            Console.WriteLine("Program.State = {0}", objIProgram.State.ToString());
            return objIProgram;
        }
        #endregion

        #region Origin

        private static StreamingEndpointAccessControl GetAccessControl()
        {
            return new StreamingEndpointAccessControl
            {
                IPAllowList = CreateIPRangeList(),

                AkamaiSignatureHeaderAuthenticationKeyList = new List<AkamaiSignatureHeaderAuthenticationKey>
                    {
                       new AkamaiSignatureHeaderAuthenticationKey
                           {
                              Identifier = "My key",
                              Expiration = DateTime.UtcNow + TimeSpan.FromDays(365),
                              Base64Key = Convert.ToBase64String(GenerateRandomBytes(16))
                           }
                    }
            };
        }

        private static byte[] GenerateRandomBytes(int length)
        {
            var bytes = new byte[length];
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                rng.GetBytes(bytes);
            }

            return bytes;
        }

        private static StreamingEndpointCacheControl GetCacheControl()
        {
            return new StreamingEndpointCacheControl
            {
                MaxAge = TimeSpan.FromSeconds(1000)
            };
        }


        public static void CreateOrigin(string originName)
        {
            StreamingEndpointCreationOptions options = new StreamingEndpointCreationOptions()
            {
                Name          = originName,
                Description   = originName,
                AccessControl = GetAccessControl(),
                CacheControl  = GetCacheControl(),
                ScaleUnits    = 1
            };
            IStreamingEndpoint myOrigin = objCloudMediaContext.StreamingEndpoints.Create(options);
            Console.WriteLine("Origin {0} has been created.", originName);
        }

        public static void ProvisionOrigin()
        {
            //We did not deploy shared origin in that environment, so you need provision dedicated RU
            IStreamingEndpoint objIStreamingEndpoint = objCloudMediaContext.StreamingEndpoints.Where(o => o.Name == "default").FirstOrDefault();
            objIStreamingEndpoint.Scale(3);  //also start it.
            Console.WriteLine("IOrigin.State = {0}, IOrigin.Id = {1}, IOrigin.HostName = {2}", objIStreamingEndpoint.State.ToString(), objIStreamingEndpoint.Id, objIStreamingEndpoint.HostName);
        }

        public static void CheckOrigin()
        {
            IStreamingEndpoint objIStreamingEndpoint = objCloudMediaContext.StreamingEndpoints.Where(o => o.Name == ORIGIN_NAME).FirstOrDefault();

            Console.WriteLine("IStreamingEndpoint.State = {0}, IStreamingEndpoint.Id = {1}, IStreamingEndpoint.ScaleUnits = {2}", objIStreamingEndpoint.State.ToString(), objIStreamingEndpoint.Id, objIStreamingEndpoint.ScaleUnits);
        }

        public static IStreamingEndpoint GetOrigin(string originName)
        {
            IStreamingEndpoint objIStreamingEndpoint = objCloudMediaContext.StreamingEndpoints.Where(o => o.Name == originName).FirstOrDefault();

            return objIStreamingEndpoint;
        }

        public static void StartOrigin(string originName)
        {
            IStreamingEndpoint objIOrigin = objCloudMediaContext.StreamingEndpoints.Where(o => o.Name == originName).FirstOrDefault();
            objIOrigin.SendStartOperation();
            Console.WriteLine(string.Format("IOrigin.State = {0}, IOrigin.HostName = {1}", objIOrigin.State.ToString(), objIOrigin.HostName));
        }

        public static void StopOrigin(string originName)
        {
            IStreamingEndpoint objIOrigin = objCloudMediaContext.StreamingEndpoints.Where(o => o.Name == originName).FirstOrDefault();
            objIOrigin.Stop();
        }

        #endregion

        #region Batch operations on channel and origins
        //bug: http://vstfpg06:8080/tfs/web/wi.aspx?pcguid=2ec59165-80d8-4a64-91ea-a249ed2c042c&id=651476
        public static void CreateChannels(bool startChannel)
        {
            IChannel objIChannel;
            foreach (string channelName in CHANNEL_NAMES)
            {
                objIChannel = CreateChannel(channelName, startChannel);
            }
        }

        public static void CreateOrigins(bool startOrigin)
        {
            foreach (string origin in CHANNEL_NAMES)
            {
                CreateOrigin(origin);
                if (startOrigin) StartOrigin(origin);
            }
        }

        public static void StartOrigins()
        {
            IStreamingEndpoint objIOrigin;
            IOperation objIOperation;
            foreach (string originName in CHANNEL_NAMES)
            {
                objIOrigin = objCloudMediaContext.StreamingEndpoints.Where(o => o.Name == originName).FirstOrDefault();
                if (objIOrigin.State == StreamingEndpointState.Stopped)
                {
                    objIOperation = objIOrigin.SendStartOperation();
                }
                Console.WriteLine("Origin {0} IOrigin.SendStartOperation has been sent.", objIOrigin.Name);
            }
        }

        public static void StopOrigins()
        {
            IStreamingEndpoint objIOrigin;
            IOperation objIOperation;
            foreach (string originName in CHANNEL_NAMES)
            {
                objIOrigin = objCloudMediaContext.StreamingEndpoints.Where(o => o.Name == originName).FirstOrDefault();
                if (objIOrigin.State == StreamingEndpointState.Running)
                {
                    objIOperation = objIOrigin.SendStopOperation();
                }
                Console.WriteLine("Origin {0} IOrigin.SendStopOperation has been sent.", objIOrigin.Name);
            }
        }

        public static void StartChannels()
        {
            IChannel objIChannel;
            IOperation objIOperation;
            foreach (string channelName in CHANNEL_NAMES)
            {
                objIChannel = objCloudMediaContext.Channels.Where(c => c.Name == channelName).FirstOrDefault();
                if (objIChannel.State == ChannelState.Stopped)
                {
                    objIOperation = objIChannel.SendStartOperation();
                }
                Console.WriteLine("Channel {0} IChannel.SendStartOperation has been sent. Ingest URL = {1}", objIChannel.Name, objIChannel.Input);
            }
        }

        public static void StopChannels()
        {
            IChannel objIChannel;
            IOperation objIOperation;
            foreach (string channelName in CHANNEL_NAMES)
            {
                objIChannel = objCloudMediaContext.Channels.Where(c => c.Name == channelName).FirstOrDefault();
                if (objIChannel.State == ChannelState.Running)
                {
                    //Delete all programs
                    foreach (IProgram prog in objIChannel.Programs)
                    {
                        if (prog.State != ProgramState.Stopped)
                        {
                            prog.Stop();
                        }
                        prog.Delete();
                        Console.WriteLine("Program {0} is deleted.", prog.Name);
                    }

                    objIOperation = objIChannel.SendStopOperation();
                }
                Console.WriteLine("Channel {0} IChannel.SendStopOperation has been sent.", objIChannel.Name);
            }
        }

        public static void DeleteChannels()
        {
            IChannel objIChannel;
            IOperation objIOperation;
            foreach (string channelName in CHANNEL_NAMES)
            {
                objIChannel = objCloudMediaContext.Channels.Where(c => c.Name == channelName).FirstOrDefault();
                foreach (IProgram objIProgram in objIChannel.Programs)
                {
                    if (objIProgram.State != ProgramState.Stopped) objIProgram.Stop();
                    objIProgram.Delete();
                }
                if (objIChannel.State == ChannelState.Stopped)
                {
                    objIOperation = objIChannel.SendDeleteOperation();
                }
                Console.WriteLine("Channel {0} IChannel.SendDeleteOperation has been sent.", objIChannel.Name);
            }
        }

        public static void DeleteOrigins()
        {
            IStreamingEndpoint objIOrigin;
            IOperation objIOperation;
            foreach (string originName in CHANNEL_NAMES)
            {
                objIOrigin = objCloudMediaContext.StreamingEndpoints.Where(o => o.Name == originName).FirstOrDefault();
                if (objIOrigin.State == StreamingEndpointState.Stopped)
                {
                    objIOperation = objIOrigin.SendDeleteOperation();
                }
                Console.WriteLine("Origin {0} IOrigin.SendDeleteOperation has been sent.", objIOrigin.Name);
            }
        }


        #endregion

        #region Daily operation in Longevity

        public static void StopStartProgramsInChannel4And5()
        {
            string[] previousPrograms = new string[] { "audiotest" };
            string[] nextPrograms     = new string[] { "audiotest" };
            string assetName          = "willzhanAsset"; 
            string[] channels         = new string[] { "aventus" };
            

            //stop previous programs
            foreach (string program in previousPrograms)
            {
                StopProgram(program, objCloudMediaContext);
            }

            //start a program on each channel
            //bool createRedundantProgram = false;  //whether to create redundant program in 2nd data center
            //string storageAccountName = System.Configuration.ConfigurationManager.AppSettings["MediaServiceStorageName"];

            //for (int i = 0; i < channels.Length; i++)
            //{
            //    objTestStream = new TestStream();
            //    //CreateProgram(channels[i], nextPrograms[i], assetName, true, nextPrograms[i].ToLower(), storageAccountName, createRedundantProgram);
            //    CreatePrograms(channels[i], nextPrograms[i], assetName, true, nextPrograms[i], createRedundantProgram);
            //    StartProgram(nextPrograms[i], objCloudMediaContext);
            //    PrintHtml();
            //}
        }

        #endregion

        #region REST API for Origin Locator

        public static void TestRestApi()
        {
            string accountName = "nbcsgliveprodwestms";
            string accountKey  = System.Configuration.ConfigurationManager.AppSettings[accountName];
            string locatorIdToReplicate = "nb:lid:UUID:b80ba82f-16a7-493d-8177-c254425af372";
            string assetId = "nb:cid:UUID:b80ba82f-16a7-493d-8177-c254425af372";
            //string acsToken    = RestApiLib.CRestApi.GetUrlEncodedAcsBearerToken(accountName, accountKey);
            //Console.WriteLine(string.Format("ACS token = {0}", System.Web.HttpUtility.UrlDecode(acsToken)));

            string locatorPath = CreateOriginLocatorWithRest(objCloudMediaContext, accountName, accountKey, locatorIdToReplicate, assetId);
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

                var accessPolicy = context.AccessPolicies.Create("RestTest", TimeSpan.FromDays(100), AccessPermissions.Read/* | AccessPermissions.List*/);
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
                        Console.WriteLine(String.Format("ILocator.Id = {0}", xmlResponse.GetElementsByTagName("Id")[0].InnerText));
                    }
                }
            }

            return locatorNewPath;
        }

        #endregion

        #region UTILITIES

        /// <summary>
        /// Add or update a single cut manifest filter by specifying clipBegin and clipEnd HNS
        /// </summary>
        /// <param name="assetId">IAsset.Id</param>
        /// <param name="objCloudMediaContext">CloudMediaContext</param>
        /// <param name="filterName">filter name</param>
        /// <param name="clipBegin">clipBegin (HNS)</param>
        /// <param name="clipEnd">clipEnd (HNS)</param>
        public static void AddUpdateManifestFilter(string assetId, CloudMediaContext objCloudMediaContext, string filterName, ulong? clipBegin, ulong? clipEnd)
        {
            ILocator objILocator = GetSasLocator(assetId, objCloudMediaContext);
            string ismfFileName = GetIsmFileName(assetId, objCloudMediaContext);
            ismfFileName += "f";
            string sasUri = GetSasUriForFile(objILocator, ismfFileName);
            Console.WriteLine(".ismf SAS URI = {0}", sasUri);

            XmlDocument objXmlDocument = null;
            FilterManager objFilterManager = new FilterManager();

            //check: if .simf file exists, download and load into FilterManager
            string ismfFileName1 = GetIsmfFileName(assetId, objCloudMediaContext);
            if (!string.IsNullOrEmpty(ismfFileName1))
            {
                try
                {
                    objXmlDocument = DownloadToXmlDocument(sasUri);
                }
                catch { }
                if (objXmlDocument != null)
                {
                    objFilterManager.ReadFilter(objXmlDocument.OuterXml);
                }
            }
            else
            {
                //add .ismf into IAsset.AssetFiles since this is used to determine if .ismf file exists
                IAsset objIAsset = GetAsset(assetId, objCloudMediaContext);
                IAssetFile objIAssetFile = objIAsset.AssetFiles.Create(ismfFileName);
            }

            //check: updating an existing filter will cause problem due to CDN caching
            bool allowFilterUpdate = true;  //set to false for production to prevent updating a filter
            bool filterExists = ManifestFilterExists(objFilterManager, filterName);
            if (!allowFilterUpdate && filterExists) return;

            //ACL takes 30 seconds to take effect
            //System.Threading.Thread.Sleep(30000);

            objFilterManager.AddOrUpdateFilter(filterName, clipBegin, clipEnd);

            //remove namespaces (needed only when creating new .ismf).
            string xml = objFilterManager.WriteFilter();
            xml = RemoveNamespace(xml);

            objXmlDocument = new XmlDocument();
            objXmlDocument.LoadXml(xml);

            UploadXmlDocument(sasUri, objXmlDocument);

            Console.WriteLine(objXmlDocument.OuterXml);
        }

        /// <summary>
        /// Determine whether single cut clipping parameters (clipBegin, clipEnd) is within the boundary of the manifest
        /// </summary>
        /// <param name="url">smooth manifest URL</param>
        /// <param name="clipBegin">clipBegin (HNS)</param>
        /// <param name="clipEnd">clipEnd (HNS)</param>
        /// <returns></returns>
        public static bool IsClipFilterValid(string url, ulong? clipBegin, ulong? clipEnd)
        {
            SmoothStreamingManifestParser objSmoothStreamingManifestParser = GetSmoothStreamingManifestParser(url);
            ulong startPosition = GetStartPositionFromManifest(objSmoothStreamingManifestParser);
            ulong endPosition = startPosition + objSmoothStreamingManifestParser.ManifestInfo.ManifestDuration;
            Console.WriteLine("Start position = {0}, End position = {1}", startPosition.ToString(), endPosition.ToString());

            clipBegin = clipBegin == null ? startPosition : clipBegin;
            clipEnd   = clipEnd   == null ? endPosition   : clipEnd;
            if (startPosition <= clipBegin && clipBegin <= clipEnd && clipEnd <= endPosition)
                return true;
            else
                return false;
        }

        private static SmoothStreamingManifestParser GetSmoothStreamingManifestParser(string url)
        {
            SmoothStreamingManifestParser objSmoothStreamingManifestParser = null;
            DownloaderManager objDownloaderManager = new DownloaderManager();
            Uri uri = new Uri(url);
            System.IO.Stream objStream = objDownloaderManager.DownloadManifest(uri, true);
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

        //e.g. https://originlongevitytestusb.blob.core.windows.net/asset-7cdbaf72-0e40-4b3e-9755-98d831a4ec25/test01.xml?sv=2012-02-12&se=2014-03-14T02%3A51%3A52Z&sr=c&si=63d955cb-606b-4026-9262-dbb62b40931b&sig=u4384d78k2zQKrWm6vyM28GXkRE8DqpH7ZNst4qxqD8%3D
        public static string GetSasUriForFile(ILocator objILocator, string filename)
        {
            return string.Format("{0}/{1}{2}", objILocator.BaseUri, filename, objILocator.ContentAccessComponent);
        }

        public static string GetOriginUri(ILocator objILocator)
        {
            var theManifest = from f in objILocator.Asset.AssetFiles where f.Name.EndsWith(".ism") select f;
            // Cast the reference to a true IAssetFile type. 
            IAssetFile objIAssetFile = theManifest.First();
            string url = string.Format("{0}{1}/manifest", objILocator.Path, objIAssetFile.Name);

            //print origin URLs
            Console.WriteLine("\tManifest file name = {0}",                 objIAssetFile.Name);
            Console.WriteLine("\tHSS: {0}{1}/manifest",                     objILocator.Path, objIAssetFile.Name);
            Console.WriteLine("\tHLS: {0}{1}/manifest(format=m3u8-aapl)",   objILocator.Path, objIAssetFile.Name);
            Console.WriteLine("\tHDS: {0}{1}/manifest(format=f4m-f4f).f4m", objILocator.Path, objIAssetFile.Name);
            Console.WriteLine("\tAssetID = {0}",                            objILocator.Asset.Id);
            Console.WriteLine("****************************************************");

            return url;
        }

        //origin URL -> locator GUID -> Asset or SAS locator
        public static string GetLocatorGuidFromOriginUrl(string originUrl)
        {
            Uri uri = new Uri(originUrl);
            string locatorGuid = uri.Segments[1].TrimEnd('/');  //origin URL format
            Guid guid;
            if (!Guid.TryParse(locatorGuid, out guid))
            {
                locatorGuid = uri.Segments[2].TrimEnd('/');     //edge URL format
                Console.WriteLine("This is an edge URL. Locator GUID = {0}", locatorGuid);
            }
            else
            {
                Console.WriteLine("This is an origin URL. Locator GUID = {0}", locatorGuid);
            }

            return locatorGuid;
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

        public static string GetIsmfFileName(string assetId, CloudMediaContext objCloudMediaContext)
        {
            IAsset objIAsset = GetAsset(assetId, objCloudMediaContext);
            IAssetFile objIAssetFile = objIAsset.AssetFiles.Where(f => f.Name.Contains(".ismf")).FirstOrDefault();

            if (objIAssetFile != null)
                return objIAssetFile.Name;
            else
                return string.Empty;
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
            Console.WriteLine("SAS locator: " + objILocator.Path);
            Console.WriteLine("Streaming asset Id: " + objIAsset.Id);
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
            string url = GetOriginUri(objILocator);

            return objILocator;
        }
        //sample .ism SAS URI: https://originlongevitytestusb.blob.core.windows.net/asset-7cdbaf72-0e40-4b3e-9755-98d831a4ec25/c29e02ec-0197-4846-9c19-c49cf7aacf00.ism?sv=2012-02-12&se=2014-03-14T02%3A51%3A52Z&sr=c&si=63d955cb-606b-4026-9262-dbb62b40931b&sig=u4384d78k2zQKrWm6vyM28GXkRE8DqpH7ZNst4qxqD8%3D
        public static XmlDocument DownloadToXmlDocument(string sasUri)
        {
            XmlDocument objXmlDocument = new XmlDocument();

            CloudBlockBlob objCloudBlockBlob = new CloudBlockBlob(new Uri(sasUri));
            System.IO.Stream objStream = new System.IO.MemoryStream();
            objCloudBlockBlob.DownloadToStream(objStream);

            //Parse it into an XML
            objStream.Position = 0;
            objXmlDocument.Load(objStream);

            return objXmlDocument;
        }

        //sasUri points to the XML file you want to upload/overwrite. If it exits, it is overwritten.
        public static void UploadXmlDocument(string sasUri, XmlDocument objXmlDocument)
        {
            System.IO.MemoryStream objMemoryStream = new System.IO.MemoryStream();
            objXmlDocument.Save(objMemoryStream);

            var objCloudBlockBlob = new CloudBlockBlob(new Uri(sasUri));
            objMemoryStream.Position = 0;
            objCloudBlockBlob.UploadFromStream(objMemoryStream);
        }

        public static IAsset GetAsset(string assetId, CloudMediaContext objCloudMediaContext)
        {
            // Use a LINQ Select query to get an asset.
            var assetInstance = from a in objCloudMediaContext.Assets where a.Id == assetId select a;
            // Reference the asset as an IAsset.
            IAsset asset = assetInstance.FirstOrDefault();

            return asset;
        }

        public static IAsset GetAssetFromOriginUrl(string url)
        {
            string originLocatorGuid = GetLocatorGuidFromOriginUrl(url);
            string originLocatorId = "nb:lid:UUID:" + originLocatorGuid;
            ILocator objILocator = objCloudMediaContext.Locators.Where(l => l.Id == originLocatorId).FirstOrDefault();
            IAsset objIAsset = objILocator.Asset;
            return objIAsset;
        }

        public static bool ManifestFilterExists(FilterManager objFilterManager, string filterName)
        {
            filtersFilter objfiltersFilter = objFilterManager.ManfiestFilter.filter.FirstOrDefault(f => f != null && string.Equals(f.name, filterName, StringComparison.CurrentCultureIgnoreCase));

            bool filterExists = (objfiltersFilter != null);
            Console.WriteLine("Filter {0} exists? {1}", filterName, filterExists.ToString());
            return filterExists;
        }

        //string xml = System.IO.File.ReadAllText("filter.xml");
        //xml = xml.Replace(" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"", "");
        //xml = xml.Replace(" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"", "");
        public static string RemoveNamespace(string xml)
        {
            XDocument objXDocument = XDocument.Parse(xml);
            
            XElement objXElement = objXDocument.Descendants("filters").FirstOrDefault();
            if (objXElement != null)
            {
                var attributes = objXElement.Attributes().Where(a => a.Name.ToString().Contains("xmlns"));
                if (attributes != null && attributes.Count() > 0)
                {
                    List<XAttribute> objList_Attribute = attributes.ToList<XAttribute>();
                    foreach (XAttribute objXAttribute in objList_Attribute)
                    {
                        objXAttribute.Remove();
                    }
                }
            }

            //full XML (XDocument removes declaration)
            xml = string.Format("{0}{1}{2}", objXDocument.Declaration.ToString(), Environment.NewLine, objXDocument.ToString());
            Console.WriteLine(xml);
            return xml;
        }

        //from origin URL (locator ID GUID) to SAS lcoator path
        public static ILocator OriginLocatorToSasLocator(string originLocatorGuid)
        {
            string originLocatorId = "nb:lid:UUID:" + originLocatorGuid;
            ILocator objILocator = objCloudMediaContext.Locators.Where(l => l.Id == originLocatorId).FirstOrDefault();
            IAsset objIAsset = objILocator.Asset;
            ILocator objILocatorSas = GetSasLocator(objIAsset.Id, objCloudMediaContext);
            return objILocatorSas;
        }

        public static void GetOriginUrls4AllProgramsInChannel(string channelName)
        {
            IAsset objIAsset;
            IAssetFile objIAssetFile;
            ILocator objILocator = null;
            IChannel objIChannel = GetChannel(channelName);
            Console.WriteLine("Programs in channel {0}, ID={1}", objIChannel.Name, objIChannel.Id);
            foreach (IProgram prog in objIChannel.Programs)
            {
                if (prog.State == ProgramState.Running)
                {
                    ColorConsole.WriteLine(string.Format("Program = {0}, \t\tStatus = {1}, \t\tID = {2}, \t\tAssetId = {3}", prog.Name, prog.State.ToString(), prog.Id, prog.AssetId), ConsoleColor.ForegroundRed | ConsoleColor.BackgroundCyan);
                }
                else
                {
                    Console.WriteLine("Program = {0}, \t\tStatus = {1}, \t\tID = {2}, \t\tAssetId = {3}", prog.Name, prog.State.ToString(), prog.Id, prog.AssetId);
                }
                objIAsset = GetAsset(prog.AssetId, objCloudMediaContext);
                
                objILocator = null;
                foreach (ILocator loc in objIAsset.Locators)
                {
                    if (loc.Type == LocatorType.OnDemandOrigin)
                    {
                        objILocator = loc;
                    }
                }
                if (objILocator == null) Console.WriteLine("Program's asset origin locator has not been created.");

                //**********************
                //ListFragBlobs(objIAsset.Id, objCloudMediaContext);
                //***************************

                var theManifest = from f in objIAsset.AssetFiles where f.Name.EndsWith(".ism") && f.IsPrimary select f;
                objIAssetFile = theManifest.First();
                //print origin URLs
                Console.WriteLine("\tManifest file name = {0}",                 objIAssetFile.Name);
                Console.WriteLine("\tHSS: {0}{1}/manifest",                     objILocator.Path, objIAssetFile.Name);
                Console.WriteLine("\tHLS: {0}{1}/manifest(format=m3u8-aapl)",   objILocator.Path, objIAssetFile.Name);
                Console.WriteLine("\tHDS: {0}{1}/manifest(format=f4m-f4f).f4m", objILocator.Path, objIAssetFile.Name);
                Console.WriteLine("\tAssetID = {0}",                            objIAsset.Id);
                Console.WriteLine("****************************************************");

                if (prog.State == ProgramState.Running)
                {
                    objTestStream = new TestStream();
                    objTestStream.OriginHssUrl0 = string.Format("{0}{1}/manifest", objILocator.Path, objIAssetFile.Name);
                    objTestStream.OriginHlsUrl0 = string.Format("{0}{1}/manifest(format=m3u8-aapl)", objILocator.Path, objIAssetFile.Name);
                    objTestStream.OriginHdsUrl0 = string.Format("{0}{1}/manifest(format=f4m-f4f).f4m", objILocator.Path, objIAssetFile.Name);
                    objTestStream.SasUrl0 = string.Empty; // GetSasLocator(prog.AssetId, objCloudMediaContext).Path;
                    objTestStream.SasUrl1 = string.Empty; //TBD

                    string edgeHost = System.Configuration.ConfigurationManager.AppSettings["EdgeHost"];
                    if (!string.IsNullOrEmpty(edgeHost))
                    {
                        //West origin URLs
                        objTestStream.OriginHssUrl1 = objTestStream.OriginHssUrl0.Replace("nbcsgliveprodeastms", "nbcsgliveprodwestms");
                        objTestStream.OriginHdsUrl1 = objTestStream.OriginHlsUrl0.Replace("nbcsgliveprodeastms", "nbcsgliveprodwestms");
                        objTestStream.OriginHdsUrl1 = objTestStream.OriginHdsUrl0.Replace("nbcsgliveprodeastms", "nbcsgliveprodwestms");

                        string path = objILocator.Path;
                        UriBuilder objUriBuilder = new UriBuilder(path);
                        path = path.Replace(objUriBuilder.Host, edgeHost + "/" + ORIGIN_NAME);

                        //East Edge URL
                        objTestStream.EdgeHssUrl0 = string.Format("{0}{1}/manifest",                     path, objIAssetFile.Name);
                        objTestStream.EdgeHlsUrl0 = string.Format("{0}{1}/manifest(format=m3u8-aapl)",   path, objIAssetFile.Name);
                        objTestStream.EdgeHdsUrl0 = string.Format("{0}{1}/manifest(format=f4m-f4f).f4m", path, objIAssetFile.Name);

                        //West Edge URL
                        string redundantEdgeHost = System.Configuration.ConfigurationManager.AppSettings["RedundantEdgeHost"];
                        objTestStream.EdgeHssUrl1 = objTestStream.EdgeHssUrl0.Replace(edgeHost, redundantEdgeHost);
                        objTestStream.EdgeHlsUrl1 = objTestStream.EdgeHlsUrl0.Replace(edgeHost, redundantEdgeHost);
                        objTestStream.EdgeHdsUrl1 = objTestStream.EdgeHdsUrl0.Replace(edgeHost, redundantEdgeHost);
                    }

                    PrintHtml();
                }
            }
        }

        public static void PrintHtml()
        {
            string htmlFormat;
            Console.WriteLine("HTML SEGMENT:");
            if (!string.IsNullOrEmpty(objTestStream.OriginHdsUrl1))
            {
                //both data centers
                htmlFormat = "<tr><td>{4}</td><td><a href=\"{0}\" target=\"_blank\">East</a>, <a href=\"{1}\" target=\"_blank\">West</a></td><td><a href=\"{2}\" target=\"_blank\">East</a>, <a href=\"{3}\" target=\"_blank\">West</a></td><td>Live</td><td>Yes</td><td></td></tr>";
                Console.WriteLine("<tr><td></td><td>Program (for {0})</td><td></td><td></td><td></td><td></td></tr>", DateTime.Now.ToString("MM/dd/yyyy"));
                Console.WriteLine(htmlFormat, objTestStream.OriginHssUrl0, objTestStream.OriginHssUrl1, objTestStream.EdgeHssUrl0, objTestStream.EdgeHssUrl1, "HSS");
                Console.WriteLine(htmlFormat, objTestStream.OriginHlsUrl0, objTestStream.OriginHlsUrl1, objTestStream.EdgeHlsUrl0, objTestStream.EdgeHlsUrl1, "HLS");
                Console.WriteLine(htmlFormat, objTestStream.OriginHdsUrl0, objTestStream.OriginHdsUrl1, objTestStream.EdgeHdsUrl0, objTestStream.EdgeHdsUrl1, "HDS");
                Console.WriteLine("<tr><td>SAS Locators:</td><td><a href=\"{0}\">East</a>, <a href=\"{1}\">West</a></td><td></td><td></td><td></td><td></td></tr>", objTestStream.SasUrl0, objTestStream.SasUrl1);
            }
            else
            {
                //only the primary data center
                htmlFormat = "<tr><td>{0}</td><td><a href=\"{1}\" target=\"_blank\">URL</a></td><td>Live</td><td>Yes</td><td></td></tr>";
                Console.WriteLine("<tr><td></td><td>Program (for {0})</td><td></td><td></td><td></td></tr>", DateTime.Now.ToString("MM/dd/yyyy"));
                Console.WriteLine(htmlFormat, "HSS", objTestStream.OriginHssUrl0);
                Console.WriteLine(htmlFormat, "HLS", objTestStream.OriginHlsUrl0);
                Console.WriteLine(htmlFormat, "HDS", objTestStream.OriginHdsUrl0);
                Console.WriteLine("<tr><td>SAS Locators:</td><td><a href=\"{0}\">SAS Locator</a></td><td></td><td></td><td></td></tr>", objTestStream.SasUrl0);
            }
        }


        #endregion

        #region Storage REST API

        //Needs to upload client certificate to Azure cert store for client authentication
        private static void TestStorageRestApi()
        {
            //initialize 3 objects
            RestApiLib.MediaServicesAccountInfo objMediaServicesAccountInfo = new RestApiLib.MediaServicesAccountInfo();
            objMediaServicesAccountInfo.MediaServicesAccountName = System.Configuration.ConfigurationManager.AppSettings["MediaServiceAccountName"];
            objMediaServicesAccountInfo.StorageAccountName       = System.Configuration.ConfigurationManager.AppSettings["MediaServiceStorageName"];
            objMediaServicesAccountInfo.StorageAccountKey        = System.Configuration.ConfigurationManager.AppSettings["MediaServiceStorageKey"];

            string endpoint       = System.Configuration.ConfigurationManager.AppSettings["Endpoint"];
            string thumbprint     = System.Configuration.ConfigurationManager.AppSettings["CertThumbprint"];
            string subscriptionId = System.Configuration.ConfigurationManager.AppSettings["SubscriptionId"];
            RestApiLib.ManagementRESTAPIHelper objManagementRESTAPIHelper = new RestApiLib.ManagementRESTAPIHelper(endpoint, thumbprint, subscriptionId);

            RestApiLib.AttachStorageAccountRequest objAttachStorageAccountRequest = new RestApiLib.AttachStorageAccountRequest();
            objAttachStorageAccountRequest.BlobStorageEndpointUri = System.Configuration.ConfigurationManager.AppSettings["BlobStorageEndpointUri2"];
            objAttachStorageAccountRequest.StorageAccountName     = System.Configuration.ConfigurationManager.AppSettings["MediaServiceStorageName2"];
            objAttachStorageAccountRequest.StorageAccountKey      = System.Configuration.ConfigurationManager.AppSettings["MediaServiceStorageKey2"];

            objManagementRESTAPIHelper.AttachStorageAccountToMediaServiceAccount(objMediaServicesAccountInfo, objAttachStorageAccountRequest);

            foreach (IStorageAccount objIStorageAccount in objCloudMediaContext.StorageAccounts)
            {
                Console.WriteLine("IStorageAccount.Name = {0}, IStorageAccount.IsDefault = {1}", objIStorageAccount.Name, objIStorageAccount.IsDefault.ToString());
            }
        }

        #endregion
    }  //class: Program

    #region Class definitions

    public class TestStream
    {
        public string SasUrl0 { get; set; }
        public string SasUrl1 { get; set; }

        public string OriginHssUrl0 { get; set; }
        public string OriginHlsUrl0 { get; set; }
        public string OriginHdsUrl0 { get; set; }

        public string EdgeHssUrl0 { get; set; }
        public string EdgeHlsUrl0 { get; set; }
        public string EdgeHdsUrl0 { get; set; }

        public string OriginHssUrl1 { get; set; }
        public string OriginHlsUrl1 { get; set; }
        public string OriginHdsUrl1 { get; set; }

        public string EdgeHssUrl1 { get; set; }
        public string EdgeHlsUrl1 { get; set; }
        public string EdgeHdsUrl1 { get; set; }
    }

    public class ProgramInfo
    {
        public IProgram Program { get; set; }
        public string LocatorPath { get; set; }       
        public string LocatorId { get; set; }         
        public string ManifestFileName { get; set; }  //primary manifest file name
        public IAsset Asset { get; set; }             //IAsset of the program
    }

    #endregion


}  //namespace: Prototypelive
