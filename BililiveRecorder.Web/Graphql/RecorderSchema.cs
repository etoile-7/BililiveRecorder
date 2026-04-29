using System;
using BililiveRecorder.Core;
using BililiveRecorder.Core.Artifacts;
using Microsoft.Extensions.DependencyInjection;
using GraphQL.Types;

namespace BililiveRecorder.Web.Graphql
{
    public class RecorderSchema : Schema
    {
        public RecorderSchema(IServiceProvider services, IRecorder recorder) : base(services)
        {
            this.Query = new RecorderQuery(recorder, services.GetRequiredService<IRecordResourceQueryService>());
            this.Mutation = new RecorderMutation(recorder);
            //this.Subscription = new RecorderSubscription();
        }
    }
}
