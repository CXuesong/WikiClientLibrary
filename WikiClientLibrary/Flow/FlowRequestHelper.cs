using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Flow
{
    internal static class FlowRequestHelper
    {
        public static async Task<Post> ReplyAsync(WikiSite site, string pageTitle, string workflowId,
            string content, CancellationToken cancellationToken)
        {
            Debug.Assert(site != null);
            Debug.Assert(pageTitle != null);
            Debug.Assert(workflowId != null);
            Debug.Assert(content != null);
            var jresult = await site.GetJsonAsync(new WikiFormRequestMessage(new
            {
                action = "flow",
                submodule = "reply",
                page = pageTitle,
                token = WikiSiteToken.Edit,
                repreplyTo = workflowId,
                repformat = "wikitext",
                repcontent = content
            }), cancellationToken);
            var jtopic = jresult["flow"]["reply"]["committed"]?["topic"];
            if (jtopic == null)
                throw new UnexpectedDataException("Missing flow.reply.committed.topic JSON node in MW API response.");
            var rep = new Post(site, pageTitle, (string)jtopic["post-id"]);
            return rep;
        }

        public static async Task<Topic> NewTopicAsync(WikiSite site, string pageTitle,
            string topicTitle, string topicContent, CancellationToken cancellationToken)
        {
            Debug.Assert(site != null);
            Debug.Assert(pageTitle != null);
            Debug.Assert(topicTitle != null);
            Debug.Assert(topicContent != null);
            var jresult = await site.GetJsonAsync(new WikiFormRequestMessage(new
            {
                action = "flow",
                submodule = "new-topic",
                page = pageTitle,
                token = WikiSiteToken.Edit,
                nttopic = topicTitle,
                ntformat = "wikitext",
                ntcontent = topicContent
            }), cancellationToken);
            var jtopiclist = jresult["flow"]["new-topic"]?["committed"]?["topiclist"];
            if (jtopiclist == null)
                throw new UnexpectedDataException("Missing flow.new-topic.committed.topiclist JSON node in MW API response.");
            var rep = new Topic(site, (string)jtopiclist["topic-page"]);
            return rep;
        }

    }
}
