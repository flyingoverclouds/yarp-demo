// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.Sample
{
    /// <summary>
    /// Initialiaztion for ASP.NET using YARP reverse proxy
    /// </summary>
    public class Startup
    {
        private const string DEBUG_HEADER = "Debug";
        private const string DEBUG_METADATA_KEY = "debug";
        private const string DEBUG_VALUE = "true";

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        public void ConfigureServices(IServiceCollection services)
        {
            // Creation dynamique des routes et clusters
            services.AddReverseProxy()
                .LoadFromMemory(GetRoutes(), GetClusters());
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.Map("/update", context =>
                {
                    context.RequestServices.GetRequiredService<InMemoryConfigProvider>().Update(GetRoutes(), GetClusters());
                    return Task.CompletedTask;
                });
                // personnalisation du pipeline de YARP
                endpoints.MapReverseProxy(proxyPipeline =>
                {
                    // Utilisation d'un pipeline custom 
                    proxyPipeline.Use(MyCustomProxyStep);

                    // On active l'affinité de Session et le LoadBalancing HTTP
                    proxyPipeline.UseSessionAffinity();
                    proxyPipeline.UseLoadBalancing();
                });
            });
        }

        /// <summary>
        /// Création dynamique d'une la Route Yarp
        /// </summary>
        /// <returns></returns>
        private RouteConfig[] GetRoutes()
        {
            return new[]
            {
                new RouteConfig()
                {
                    RouteId = "route" + Random.Shared.Next(), // Forces a new route id each time GetRoutes is called.
                    ClusterId = "cluster1",
                    Match = new RouteMatch
                    {
                        // Path or Hosts are required for each route. This catch-all pattern matches all request paths.
                        Path = "{**catch-all}"
                    }
                }
            };
        }

        /// <summary>
        /// Creation dynamique du cluster 
        /// </summary>
        /// <returns></returns>
        private ClusterConfig[] GetClusters()
        {
            var debugMetadata = new Dictionary<string, string>();
            debugMetadata.Add(DEBUG_METADATA_KEY, DEBUG_VALUE);

            return new[]
            {
                new ClusterConfig()
                {
                    ClusterId = "cluster1",
                    SessionAffinity = new SessionAffinityConfig { Enabled = true, Policy = "Cookie", AffinityKeyName = ".Yarp.ReverseProxy.Affinity" },
                    Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "destination1", new DestinationConfig() { Address = "https://example.com" } },
                        { "debugdestination1", new DestinationConfig() {
                            Address = "https://bing.com",
                            Metadata = debugMetadata  }
                        },
                    }
                }
            };
        }


        /// <summary>
        /// Custom proxy step that filters destinations based on a header in the inbound request
        /// Looks at each destination metadata, and filters in/out based on their debug flag and the inbound header
        /// </summary>
        public Task MyCustomProxyStep(HttpContext context, Func<Task> next)
        {
            // Can read data from the request via the context
            var useDebugDestinations = context.Request.Headers.TryGetValue(DEBUG_HEADER, out var headerValues) && headerValues.Count == 1 && headerValues[0] == DEBUG_VALUE;

            // The context also stores a ReverseProxyFeature which holds proxy specific data such as the cluster, route and destinations
            var availableDestinationsFeature = context.Features.Get<IReverseProxyFeature>();
            var filteredDestinations = new List<DestinationState>();

            // Filter destinations based on criteria
            foreach (var d in availableDestinationsFeature.AvailableDestinations)
            {
                //Todo: Replace with a lookup of metadata - but not currently exposed correctly here
                if (d.DestinationId.Contains("debug") == useDebugDestinations) { filteredDestinations.Add(d); }
            }
            availableDestinationsFeature.AvailableDestinations = filteredDestinations;

            // Important - required to move to the next step in the proxy pipeline
            return next();
        }
    }
}
