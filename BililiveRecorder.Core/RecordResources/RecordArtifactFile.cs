namespace BililiveRecorder.Core.Artifacts
{
    public sealed class RecordArtifactFile
    {
        public string Role { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
    }
}
