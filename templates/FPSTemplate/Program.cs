#define FPS
using Engine.Utils;
using System.Reflection;

class Startup
{
    static void Main(string[] args)
    {
        GameStartup.Run<GameEngine>(Assembly.GetExecutingAssembly(), args);
    }
}