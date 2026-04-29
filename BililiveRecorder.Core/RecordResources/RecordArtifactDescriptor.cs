namespace BililiveRecorder.Core.Artifacts
{
    public sealed class RecordArtifactDescriptor
    {
        public string Role { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Container { get; set; } = string.Empty;
        public long Size { get; set; }
    }
}
