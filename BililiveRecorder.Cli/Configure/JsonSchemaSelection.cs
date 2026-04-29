using System.ComponentModel;

namespace BililiveRecorder.Cli.Configure
{
    public enum JsonSchemaSelection
    {
        [Description("https://raw.githubusercontent.com/etoile-7/BililiveRecorder/dev/configV3.schema.json")]
        Default,

        [Description("Custom")]
        Custom
    }
}
