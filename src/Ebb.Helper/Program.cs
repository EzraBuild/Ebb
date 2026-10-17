using Ebb.Core.Elevation;
using Ebb.Core.Platform;

// Ebb.Helper runs with admin rights for the few steps that need them. It only accepts the fixed
// operations in HelperArgs and reports back through a JSON file that Ebb reads.

if (!HelperArgs.TryParse(args, out var request, out var resultPath, out var error))
{
    if (resultPath is not null)
        HelperArgs.WriteResult(resultPath, new HelperResult(false, error ?? "Bad arguments."));
    return 2;
}

HelperResult result;
try
{
    result = await new HelperOperations(new ProcessCommandRunner()).RunAsync(request);
}
catch (Exception ex)
{
    result = new HelperResult(false, ex.Message);
}

HelperArgs.WriteResult(resultPath, result);
return result.Succeeded ? 0 : 1;
