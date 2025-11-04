using Minio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MinioProvider.Helper
{
    public interface IMinioHelper
    {
        Task<string> InsertFile(string fileName, Stream file, string bucketName);
        Task<byte[]> GetFile(string fileName, string bucketName);
        Task<bool> CreateBucket(string bucketName);
    }
}
