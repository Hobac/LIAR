using LIAR_backend.Expressions;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LIAR_backend
{
    public class Server
    {
        private readonly HttpListener listener = new();

        public async Task Run()
        {
            listener.Prefixes.Add("http://localhost:8080/");
            listener.Start();

            Info.Write("Server listening on http://localhost:8080/");

            while (true)
            {
                HttpListenerContext context = await listener.GetContextAsync();

                // start handling, don't wait
                // immediately continue loop
                _ = HandleRequest(context); 
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            // The frontend is served separately during development, so browser
            // requests require CORS headers and an OPTIONS preflight response.
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            try
            {
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 204;
                }
                else if (request.HttpMethod == "GET" && request.Url?.AbsolutePath == "/health")
                {
                    byte[] data = Encoding.UTF8.GetBytes("{\"status\":\"ok\"}");

                    response.StatusCode = 200;
                    response.ContentType = "application/json";
                    response.ContentEncoding = Encoding.UTF8;
                    response.ContentLength64 = data.Length;

                    await response.OutputStream.WriteAsync(data);
                }
                else if (request.HttpMethod == "POST" && request.Url?.AbsolutePath == "/check")
                {
                    using StreamReader reader = new(request.InputStream, request.ContentEncoding);

                    string input = await reader.ReadToEndAsync();
                    string json = await Check(input);
                    byte[] data = Encoding.UTF8.GetBytes(json);

                    response.StatusCode = 200;
                    response.ContentType = "application/json";
                    response.ContentEncoding = Encoding.UTF8;
                    response.ContentLength64 = data.Length;

                    await response.OutputStream.WriteAsync(data);
                }
                else
                {
                    response.StatusCode = 404;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);

                response.StatusCode = 500;

                byte[] data = Encoding.UTF8.GetBytes("{\"error\":\"Internal server error\"}");
                response.ContentType = "application/json";
                response.ContentLength64 = data.Length;

                await response.OutputStream.WriteAsync(data);
            }
            finally
            {
                response.Close();
            }
        }

        private async Task<string> Check(string input)
        {
            Info.WriteStart("Statement extraction");
            List<Statement> statements = await Pipeline.ExtractStatements(input);
            Info.WriteStop();
            Info.WriteUpdate(statements);

            Info.WriteStart("Translation to first-order logic");
            await Pipeline.GetFirstOrderLogic(statements);
            Info.WriteStop();
            Info.WriteUpdate(statements);

            Info.WriteStart("Resolve first-order formula");
            await Pipeline.ResolveFirstOrderLogic(statements);
            Info.WriteStop();
            Info.WriteUpdate(statements);

            Info.WriteStart("Eliminate forall quantifier");
            Pipeline.EliminateForall(statements);
            Info.WriteStop();
            Info.WriteUpdate(statements);

            Info.WriteStart("Evaluation");
            await Pipeline.EvaluateStatements(statements);
            Info.WriteStop();
            Info.WriteUpdate(statements);

            Info.WriteUpdate(statements);
            Info.PrintColoredText(input, statements);

            return JsonSerializer.Serialize(statements);
        }
    }
}
