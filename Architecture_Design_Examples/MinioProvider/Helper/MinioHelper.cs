using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MinioProvider.Helper
{
    /// <summary>
    /// Provides helper methods to interact with a MinIO server.
    /// Supports bucket management and object upload/download operations.
    /// </summary>
    public class MinioHelper : IMinioHelper
    {
        private readonly MinioConfigurations _configurations;

        /// <summary>
        /// Initializes a new instance of the <see cref="MinioHelper"/> class.
        /// </summary>
        /// <param name="conf">MinIO configuration containing endpoint and credentials.</param>
        public MinioHelper(MinioConfigurations conf)
        {
            _configurations = conf ?? throw new ArgumentNullException(nameof(conf));
        }

        /// <summary>
        /// Creates and returns a configured <see cref="IMinioClient"/> instance.
        /// </summary>
        private IMinioClient CreateClient()
        {
            var endpoint = _configurations.Endpoint.Replace("https://", "", StringComparison.OrdinalIgnoreCase);
            return new MinioClient()
                .WithSSL(false)
                .WithEndpoint(endpoint)
                .WithCredentials(_configurations.UserName, _configurations.Password)
                .Build();
        }

        /// <summary>
        /// Creates a bucket in MinIO if it does not already exist.
        /// </summary>
        /// <param name="bucketName">Name of the bucket to create.</param>
        /// <returns><c>true</c> if the bucket exists or was created successfully; otherwise <c>false</c>.</returns>
        public async Task<bool> CreateBucket(string bucketName)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty.", nameof(bucketName));

            using var client = CreateClient();

            try
            {
                bool exists = await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName));
                if (!exists)
                    await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName));

                return true;
            }
            catch (MinioException)
            {
                return false;
            }
        }

        /// <summary>
        /// Uploads a file stream to a specified MinIO bucket.
        /// </summary>
        /// <param name="fileName">Name of the file to be stored in MinIO.</param>
        /// <param name="file">Stream containing the file data.</param>
        /// <param name="bucketName">Target bucket name.</param>
        /// <returns>ETag string if upload is successful, otherwise an empty string.</returns>
        public async Task<string> InsertFile(string fileName, Stream file, string bucketName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            if (file == null || file.Length == 0)
                throw new ArgumentException("Invalid file stream.", nameof(file));

            string contentType = GetContentType(fileName);

            using var client = CreateClient();

            try
            {
                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(fileName)
                    .WithStreamData(file)
                    .WithObjectSize(file.Length)
                    .WithContentType(contentType);

                var response = await client.PutObjectAsync(putObjectArgs);
                return response.Etag ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Retrieves a file from a specified MinIO bucket as a byte array.
        /// </summary>
        /// <param name="fileName">Name of the file to retrieve.</param>
        /// <param name="bucketName">Name of the bucket containing the file.</param>
        /// <returns>Byte array containing the file data, or <c>null</c> if not found.</returns>
        public async Task<byte[]> GetFile(string fileName, string bucketName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            using var client = CreateClient();
            byte[] buffer = null;

            try
            {
                var args = new GetObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(fileName)
                    .WithCallbackStream(stream =>
                    {
                        using var mem = new MemoryStream();
                        stream.CopyTo(mem);
                        if (mem.Length > 0)
                            buffer = mem.ToArray();
                    });

                await client.GetObjectAsync(args, CancellationToken.None);
            }
            catch (ObjectNotFoundException)
            {
                return null;
            }
            catch
            {
                throw;
            }

            return buffer;
        }

        /// <summary>
        /// Determines the MIME type based on the file extension.
        /// </summary>
        private static string GetContentType(string fileName)
        {
            var lower = fileName.ToLowerInvariant();

            return lower switch
            {
                var s when s.EndsWith(".jpeg") || s.EndsWith(".jpg") => "image/jpeg",
                var s when s.EndsWith(".png") => "image/png",
                var s when s.EndsWith(".mp4") => "video/mp4",
                var s when s.EndsWith(".pdf") => "application/pdf",
                _ => "application/octet-stream"
            };
        }
    }
}
