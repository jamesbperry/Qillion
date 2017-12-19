using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OSIResearch.Qillion.Functions
{
    static class BindingRedirector
    {
        //Lifted from npiasecki's workaround for the lack of native binding redirects
        //https://github.com/Azure/azure-webjobs-sdk-script/issues/992
        public static void RedirectAssembly(
            string shortName,
            Version targetVersion,
            string publicKeyToken)
        {
            ResolveEventHandler handler = null;

            handler = (sender, args) =>
            {
                var requestedAssembly = new AssemblyName(args.Name);

                if (requestedAssembly.Name != shortName)
                {
                    return null;
                }

                var targetPublicKeyToken = new AssemblyName("x, PublicKeyToken=" + publicKeyToken)
                    .GetPublicKeyToken();
                requestedAssembly.Version = targetVersion;
                requestedAssembly.SetPublicKeyToken(targetPublicKeyToken);
                requestedAssembly.CultureInfo = CultureInfo.InvariantCulture;

                AppDomain.CurrentDomain.AssemblyResolve -= handler;

                return Assembly.Load(requestedAssembly);
            };

            AppDomain.CurrentDomain.AssemblyResolve += handler;
        }
    }
}
