using Nancy;

namespace Codex.Web.Modules
{
    public class IndexModule : NancyModule
    {
        public IndexModule()
        {
            Get["/"] = _ => View["bin/view/index.min.html"];
            //Get["/"] = _ => "Hello World";
        }
    }
}