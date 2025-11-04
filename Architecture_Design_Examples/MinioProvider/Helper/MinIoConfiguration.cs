namespace MinIoConnectionLayer.Helper
{
    public class MinioConfigurations
    {
        public MinioConfigurations(string endpoint, string userName, string password)
        {
            Endpoint = endpoint;
            UserName = userName;
            Password = password;
        }

        public string Endpoint { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}