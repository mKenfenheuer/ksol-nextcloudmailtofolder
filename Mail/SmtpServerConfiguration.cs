namespace KSol.NextCloudMailToFolder.Mail
{
    public class SmtpServerConfiguration
    {
        public SmtpServerEndPoint[]? EndPoints { get; set; }
        public string? Hostname { get; set; }
        public class SmtpServerEndPoint
        {
            public string? Address { get; set; }
            public int Port { get; set; }
        }
    }
}
