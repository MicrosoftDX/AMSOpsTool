using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.MediaServices.Client.ContentKeyAuthorization;
using Microsoft.WindowsAzure.MediaServices.Client.DynamicEncryption;

namespace PrototypeConsole
{
    public class AesEncryption
    {
        //Key Delivery URL = https://willzhanmediaservice.keydelivery.mediaservices.windows.net/?KID=7dfa376f-8a29-44f1-b546-3215492fe12f
        //You must publish AFTER you configure AES dynamic encryption. Segment manifest will contain encryption info.
        public static void DynamicAesEncryptionFlow(CloudMediaContext objCloudMediaContext, IAsset objIAsset)
        {
            //Create IContentKey
            IContentKey objIContentKey = CreateEnvelopeTypeContentKey(objCloudMediaContext);

            //add AuthorizationPolicy to IContentKey
            objIContentKey = AddAuthorizationPolicyToContentKey(objCloudMediaContext, objIContentKey, objIAsset.Id);

            //create asset delivery policy
            IAssetDeliveryPolicy objIAssetDeliveryPolicy = CreateAssetDeliveryPolicy(objCloudMediaContext, objIContentKey);

            //Associate IContentKey with IAsset
            objIAsset.ContentKeys.Add(objIContentKey);

            // Add AssetDelivery Policy to the asset
            objIAsset.DeliveryPolicies.Add(objIAssetDeliveryPolicy);
        }
        static public IContentKey CreateEnvelopeTypeContentKey(CloudMediaContext objCloudMediaContext)
        {
            // Create envelope encryption content key
            Guid keyId = Guid.NewGuid();
            byte[] contentKey = CryptoUtils.GenerateCryptographicallyStrongRandomBytes(16);

            IContentKey objIContentKey = objCloudMediaContext.ContentKeys.Create(keyId,
                                                                                 contentKey,
                                                                                 "HLSContentKey",
                                                                                 ContentKeyType.EnvelopeEncryption);

            return objIContentKey;
        }

        public static IContentKey AddAuthorizationPolicyToContentKey(CloudMediaContext objCloudMediaContext, IContentKey objIContentKey, string keyId)
        {
            // Create ContentKeyAuthorizationPolicy with restrictions and create authorization policy             
            IContentKeyAuthorizationPolicy policy = objCloudMediaContext.ContentKeyAuthorizationPolicies.CreateAsync(keyId).Result;

            List<ContentKeyAuthorizationPolicyRestriction> restrictions = new List<ContentKeyAuthorizationPolicyRestriction>();

            //ContentKeyAuthorizationPolicyRestriction restriction = new ContentKeyAuthorizationPolicyRestriction
            //                                                                            {
            //                                                                                Name = "Open Authorization Policy",
            //                                                                                KeyRestrictionType = (int)ContentKeyRestrictionType.Open,
            //                                                                                Requirements = null // no requirements
            //                                                                            };
            //ContentKeyAuthorizationPolicyRestriction restriction = new ContentKeyAuthorizationPolicyRestriction
            //{
            //    Name = "Authorization Policy with SWT Token Restriction",
            //    KeyRestrictionType = (int)ContentKeyRestrictionType.TokenRestricted,
            //    Requirements = ContentKeyAuthorizationHelper.CreateRestrictionRequirements()
            //};
            ContentKeyAuthorizationPolicyRestriction restriction = new ContentKeyAuthorizationPolicyRestriction
            {
                Name = "JWTContentKeyAuthorizationPolicyRestriction",
                KeyRestrictionType = (int)ContentKeyRestrictionType.TokenRestricted,
                Requirements = ContentKeyAuthorizationHelper.CreateRestrictionRequirementsForJWT()
            };

            restrictions.Add(restriction);

            IContentKeyAuthorizationPolicyOption policyOption = objCloudMediaContext.ContentKeyAuthorizationPolicyOptions.Create(
                                                                                        keyId,
                                                                                        ContentKeyDeliveryType.BaselineHttp,
                                                                                        restrictions,
                                                                                        "");

            policy.Options.Add(policyOption);

            // Add ContentKeyAutorizationPolicy to ContentKey
            objIContentKey.AuthorizationPolicyId = policy.Id;
            IContentKey IContentKeyUpdated = objIContentKey.UpdateAsync().Result;

            return IContentKeyUpdated;
        }

        public static IAssetDeliveryPolicy CreateAssetDeliveryPolicy(CloudMediaContext objCloudMediaContext, IContentKey objIContentKey)
        {
            Uri keyAcquisitionUri = objIContentKey.GetKeyDeliveryUrl(ContentKeyDeliveryType.BaselineHttp);

            string envelopeEncryptionIV = Convert.ToBase64String(CryptoUtils.GenerateCryptographicallyStrongRandomBytes(16));

            // The following policy configuration specifies: 
            //   key url that will have KID=<Guid> appended to the envelope and
            //   the Initialization Vector (IV) to use for the envelope encryption.
            Dictionary<AssetDeliveryPolicyConfigurationKey, string> assetDeliveryPolicyConfiguration = new Dictionary<AssetDeliveryPolicyConfigurationKey, string> 
                                            {
                                                {AssetDeliveryPolicyConfigurationKey.EnvelopeKeyAcquisitionUrl, keyAcquisitionUri.ToString()},
                                                {AssetDeliveryPolicyConfigurationKey.EnvelopeEncryptionIVAsBase64, envelopeEncryptionIV}
                                            };

            IAssetDeliveryPolicy objIAssetDeliveryPolicy = objCloudMediaContext.AssetDeliveryPolicies.Create(
                                                                                            "SmoothHLSDynamicEncryptionAssetDeliveryPolicy",
                                                                                            AssetDeliveryPolicyType.DynamicEnvelopeEncryption,
                                                                                            AssetDeliveryProtocol.SmoothStreaming | AssetDeliveryProtocol.HLS | AssetDeliveryProtocol.Dash,
                                                                                            assetDeliveryPolicyConfiguration);

            // Add AssetDelivery Policy to the asset
            //objIAsset.DeliveryPolicies.Add(objIAssetDeliveryPolicy);
            Console.WriteLine();
            Console.WriteLine("Adding Asset Delivery Policy: " + objIAssetDeliveryPolicy.AssetDeliveryPolicyType);
            Console.WriteLine("Key Delivery URL = {0}", keyAcquisitionUri.ToString());

            return objIAssetDeliveryPolicy;
        } 

    }
}
