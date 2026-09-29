using LIAR_backend.Expressions;
using System.Text.Json;

namespace LIAR_backend
{
    internal class Program
    {
        // Future projects:
        // - Introduce mixed quantifers

        static async Task Main(string[] args)
        {
            Server server = new Server();
            await server.Run();
        }
    }
}
