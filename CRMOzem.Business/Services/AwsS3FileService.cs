using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using CRMOzem.Business.Interfaces;
using Amazon;

namespace CRMOzem.Business.Services
{
    public class AwsS3FileService : IAwsS3FileService
    {
        private readonly string _bucketName;
        private readonly IAmazonS3 _s3Client;

        public AwsS3FileService(IConfiguration configuration)
        {
            var awsOptions = configuration.GetSection("AWS");
            _bucketName = awsOptions["BucketName"];
            _s3Client = new AmazonS3Client(
                awsOptions["AccessKey"],
                awsOptions["SecretKey"],
                RegionEndpoint.GetBySystemName(awsOptions["Region"])
            );
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string folder)
        {
            var key = CombineS3Path(folder, fileName);  // folder zaten tam path olmalı
            var request = new TransferUtilityUploadRequest
            {
                InputStream = fileStream,
                BucketName = _bucketName,
                Key = key
            };

            var transferUtility = new TransferUtility(_s3Client);
            await transferUtility.UploadAsync(request);

            return $"https://{_bucketName}.s3.{_s3Client.Config.RegionEndpoint.SystemName}.amazonaws.com/{key}";
        }

        public async Task<bool> DeleteFileAsync(string fileName, string folder)
        {
            var key = CombineS3Path(folder, fileName);
            var response = await _s3Client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucketName, Key = key });
            return response.HttpStatusCode == System.Net.HttpStatusCode.NoContent;
        }
        public async Task CreateFolderAsync(string parentFolder, string folderName)
        {
            var key = CombineS3Path(parentFolder, folderName) + "/";
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                ContentBody = string.Empty
            };
            await _s3Client.PutObjectAsync(request);
        }

        public async Task<List<string>> ListFilesAsync(string folder)
        {
            var prefix = string.IsNullOrEmpty(folder) ? "" : $"{folder}/";  // Tam alt klasör yolu
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix
            };

            var response = await _s3Client.ListObjectsV2Async(request);

            return response.S3Objects
                .Where(o => !o.Key.EndsWith("/"))  // Sadece dosyaları al
                .Select(o => $"https://{_bucketName}.s3.{_s3Client.Config.RegionEndpoint.SystemName}.amazonaws.com/{o.Key}")
                .ToList();
        }

        public async Task<List<string>> ListFoldersAsync(string folder)
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = string.IsNullOrEmpty(folder) ? "" : $"{folder}/",
                Delimiter = "/"
            };

            var response = await _s3Client.ListObjectsV2Async(request);

            return response.CommonPrefixes
                .Select(p => p.TrimEnd('/').Split('/').Last())
                .ToList();
        }


        public async Task<Stream?> DownloadFileAsync(string fileName, string folder)
        {
            var key = $"{folder}/{fileName}";
            try
            {
                var response = await _s3Client.GetObjectAsync(_bucketName, key);
                return response.ResponseStream;
            }
            catch (AmazonS3Exception)
            {
                return null;
            }
        }

        private static string GetContentType(string fileName)
        {
            return Path.GetExtension(fileName).ToLower() switch
            {
                ".jpg" => "image/jpeg",
                ".png" => "image/png",
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/octet-stream"
            };
        }
        private string CombineS3Path(params string[] parts)
        {
            return string.Join("/", parts.Where(p => !string.IsNullOrEmpty(p)));
        }
    }
}
