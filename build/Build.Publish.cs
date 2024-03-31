using Nuke.Common;

partial class Build
{
    Target Publish => _ => _
        .DependsOn(Installer, Bundle)
        .Executes(() =>
        {
            
        });
}
