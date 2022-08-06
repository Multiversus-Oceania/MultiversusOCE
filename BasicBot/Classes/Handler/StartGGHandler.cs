using System;
using System.Collections.Generic;
using System.Net.Http;
using BasicBot.GraphQL.EventId;
using BasicBot.GraphQL.SetsAndLinkedAccounts;
using RestSharp;
using Data = BasicBot.GraphQL.SetsAndLinkedAccounts.Data;

namespace BasicBot.Handler;

public static class StartGGHandler
{
    public class StartGGParameter
    {
        public string Value;

        private StartGGParameter(string value)
        {
            Value = value.Replace("  ", "");
        }

        public static implicit operator StartGGParameter(string str)
        {
            if (str.Trim() == "")
                return null;

            return new StartGGParameter(str);
        }
    }


    public interface IStartGGQuery
    {
        public StartGGParameter Query();

        public StartGGParameter Variables();
    }

    public class GetSetsAndLinkedAccounts : IStartGGQuery
    {
        public Data Data;

        private readonly string EventId;
        private readonly int Page;
        private readonly int PerPage;

        public GetSetsAndLinkedAccounts(string eventId, int page, int perPage)
        {
            EventId = eventId;
            Page = page;
            PerPage = perPage;

            var a = this.Execute();
            if (a.IsSuccessful)
                Data = SetsAndLinkedAccounts.FromJson(a.Content).Data;
            else
                throw new HttpRequestException(a.ErrorMessage, null, a.StatusCode);
        }

        StartGGParameter IStartGGQuery.Variables()
        {
            return $@"{{
                ""eventId"": {EventId},
                ""page"": {Page},
                ""perPage"": {PerPage}
            }}";
        }

        StartGGParameter IStartGGQuery.Query()
        {
            #region Query

            return @"
            query GetSetsAndLinkedAccounts( $eventId: ID, $page: Int,$perPage: Int){
                currentUser {
                    id
                }
                event(id: $eventId){
                    id
                    tournament {
                        admins
                        {
                            id
                        }
                    }
                    sets(page: $page, perPage: $perPage, filters: { showByes: false, hideEmpty: true, state: [1,2] }){
                        nodes {
                            id
                            totalGames
                            state
                            winnerId
                            games {
                                winnerId
                                orderNum
                                stage {
                                    name
                                }
                            }
                            slots{
                                entrant {
                                    id
                                    name
                                    participants {
                                        gamerTag
                                        requiredConnections {
                                            id
                                            externalId
                                            externalUsername
                                            type
                                            url
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            ";

            #endregion
        }
    }

    public class GetEventId : IStartGGQuery
    {
        private readonly string Slug;

        public GraphQL.EventId.Data Data;

        public GetEventId(string slug)
        {
            Slug = slug;
            var a = this.Execute();

            if (a.IsSuccessful)
                Data = EventId.FromJson(a.Content).Data;
            else
                throw new HttpRequestException(a.ErrorMessage, null, a.StatusCode);
        }

        StartGGParameter IStartGGQuery.Variables()
        {
            return $@"{{
                ""slug"": ""{Slug}""
            }}";
        }

        StartGGParameter IStartGGQuery.Query()
        {
            #region Query

            return @"
            query GetEventId( $slug: String ){
                event(slug: $slug){
                    id
                    name
                }
            }
            ";

            #endregion
        }
    }

    public static List<(string, string)> GetParameters(this IStartGGQuery query)
    {
        var parameters = new List<(string, string)>();

        if (query.Query() is StartGGParameter prmQ)
            parameters.Add(new ValueTuple<string, string>("query", prmQ.Value));

        if (query.Variables() is StartGGParameter prmV)
            parameters.Add(new ValueTuple<string, string>("variables", prmV.Value));

        return parameters;
    }

    public static RestResponse Execute(this IStartGGQuery query)
    {
        var url = "https://api.start.gg/gql/alpha";

        return Send(url,
            query.GetParameters()
        );
    }

    private static RestResponse Send(string url, List<(string, string)> parameters = null, Method type = Method.Get)
    {
        var client = new RestClient(url);
        client.AddDefaultHeader("Authorization", "Bearer " + Settings.GetSettings().StartGGToken);

        var request = new RestRequest();
        request.Method = type;

        if (parameters != null)
            foreach (var a in parameters)
            {
                request.AddParameter(a.Item1, a.Item2);
            }

        var response = client.Execute(request);

        return response;
    }
}