using Ebb.Core.Cleanup;
using static Ebb.Tests.FakeLayout;

namespace Ebb.Tests.Cleanup;

public class PathGuardTests : IDisposable
{
    private readonly FakeLayout _layout = new();
    private readonly PathGuard _guard;

    public PathGuardTests() => _guard = new PathGuard(_layout.Paths, confineTo: _layout.Sandbox.Root);

    public void Dispose() => _layout.Dispose();

    [Theory]
    [InlineData(@"Users\me\Downloads\setup.exe")]
    [InlineData(@"Users\me\AppData\Local\Temp\old.tmp")]
    [InlineData(@"Users\me\AppData\Local\npm-cache\_cacache")]
    [InlineData(@"src\app\node_modules")]
    public void Allows_things_inside_your_folders(string relative)
    {
        Create(relative);
        Assert.Null(_guard.Check(_layout.PathOf(relative)));
    }

    [Theory]
    [InlineData(@"Windows\Temp\x.tmp")]
    [InlineData(@"Windows\System32\drivers\etc\hosts")]
    [InlineData(@"Program Files\dotnet\sdk\8.0.100")]
    [InlineData(@"Program Files (x86)\App\app.exe")]
    [InlineData(@"ProgramData\Package Cache\setup.msi")]
    public void Refuses_windows_and_install_folders(string relative)
    {
        Create(relative);
        Assert.Contains("never touched", _guard.Check(_layout.PathOf(relative)));
    }

    [Theory]
    [InlineData("hiberfil.sys")]
    [InlineData("src")]
    [InlineData("Users")]
    public void Refuses_top_level_items(string relative)
    {
        Create(relative);
        Assert.Contains("Top-level", _guard.Check(_layout.PathOf(relative)));
    }

    [Theory]
    [InlineData(@"Users\me")]
    [InlineData(@"Users\me\Downloads")]
    [InlineData(@"Users\me\Documents")]
    [InlineData(@"Users\me\AppData")]
    [InlineData(@"Users\me\AppData\Local")]
    [InlineData(@"Users\me\AppData\Local\Temp")]
    public void Refuses_your_main_folders_themselves(string relative)
    {
        _layout.Dir(relative);
        Assert.NotNull(_guard.Check(_layout.PathOf(relative)));
    }

    [Fact]
    public void Refuses_other_users_files()
    {
        Create(@"Users\other\Documents\diary.txt");
        Assert.Equal("Belongs to another user.", _guard.Check(_layout.PathOf(@"Users\other\Documents\diary.txt")));
    }

    [Fact]
    public void Refuses_anything_outside_the_sandbox()
    {
        Assert.Equal("Outside the allowed folder.", _guard.Check(@"C:\Windows\notepad.exe"));
        Assert.Equal("Outside the allowed folder.", _guard.Check(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)));
    }

    [Fact]
    public void Refuses_system_files_links_and_windows_managed_folders()
    {
        var system = _layout.File(@"Users\me\Desktop\desktop.ini", 10);
        File.SetAttributes(system, FileAttributes.Hidden | FileAttributes.System);
        var target = _layout.Dir(@"Users\me\Documents\real");
        var link = _layout.Sandbox.Junction(@"Users\me\Documents\link", target);
        _layout.File(@"src\$Recycle.Bin\S-1-5\$R123.txt", 10);

        Assert.Contains("system file", _guard.Check(system));
        Assert.Contains("link", _guard.Check(link));
        Assert.Equal("Windows manages this folder.", _guard.Check(_layout.PathOf(@"src\$Recycle.Bin\S-1-5\$R123.txt")));
    }

    [Theory]
    [InlineData(@"relative\path.txt")]
    [InlineData(@"C:\Users\*\Downloads")]
    public void Refuses_relative_paths_and_wildcards(string path) =>
        Assert.Equal("Not a full path.", _guard.Check(path));

    [Fact]
    public void Refuses_missing_paths()
    {
        Assert.Equal("It's already gone.", _guard.Check(_layout.PathOf(@"Users\me\Downloads\nope.exe")));
    }

    private void Create(string relative)
    {
        if (Path.HasExtension(relative))
            _layout.File(relative, 10);
        else
            _layout.Dir(relative);
    }
}
