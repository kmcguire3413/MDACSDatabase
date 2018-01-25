using MDACS.Server;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace MDACS.Database
{
    static class HandleLocalWebRes
    {
        public static async Task<Task> Index(
            ServerHandler state,
            HTTPRequest request,
            Stream body,
            IProxyHTTPEncoder encoder)
        {
            await Util.ReadStreamUntilEndAndDiscardDataAsync(body);

            return await StaticRoute("index.html", state, request, body, encoder);
        }

        /// <summary>
        /// Helper method used to send data stored as a resource under the webres folder (namespace).
        /// </summary>
        /// <param name="target">The name of the resource in the webres folder/namespace.</param>
        /// <param name="state">Pass-through parameter.</param>
        /// <param name="request">Pass-through parameter.</param>
        /// <param name="body">Pass-through parameter.</param>
        /// <param name="encoder">Pass-through parameter.</param>
        /// <returns>A task object which may or may not be completed already. This also may need to be returned as a dependency of the handler completion.</returns>
        private static async Task<Task> StaticRoute(
            string target,
            ServerHandler state,
            HTTPRequest request,
            Stream body,
            IProxyHTTPEncoder encoder)
        {
            await Util.ReadStreamUntilEndAndDiscardDataAsync(body);

#if USE_SOURCE_DIRECTORY_WEBRES
            var strm = File.OpenRead(
                Path.Combine(
                    @"/home/kmcguire/extra/old/source/repos/MDACSDatabase/MDACSDatabase/webres",
                    target
                )
            );
#else
            var strm = Assembly.GetExecutingAssembly().GetManifestResourceStream($"MDACSDatabase.webres.{target}");

            if (strm == null)
            {
                return await encoder.Response(404, "Not Found")
                    .CacheControl("no-cache, no-store, must-revalidate")
                    .SendNothing();
            }
#endif
            return await encoder.Response(200, "OK")
                .ContentType_GuessFromFileName(target)
                .CacheControl("public, max-age=0")
                .SendStream(strm);
        }

        public static async Task<Task> Utility(
            ServerHandler state,
            HTTPRequest request,
            Stream body,
            IProxyHTTPEncoder encoder)
        {
            await Util.ReadStreamUntilEndAndDiscardDataAsync(body);

            return await StaticRoute(request.query_string, state, request, body, encoder);
        }
    }
}