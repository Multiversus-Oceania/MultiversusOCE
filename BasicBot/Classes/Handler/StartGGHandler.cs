using System;
using System.Net.Http.Headers;
using System.Text.Json;
using BasicBot.GraphQL;
using Microsoft.Extensions.DependencyInjection;
using StrawberryShake.Serialization;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace BasicBot.Handler;

public class StartGGHandler
{
    public static IStartGGClient Client
    {
        get
        {
            if (client == null)
            {
                Initialize();
            }

            return client;
        }
    }

    private static IStartGGClient client;
    private static ServiceCollection serviceCollection;
    private static ServiceProvider serviceProvider;

    private static void Initialize()
    {
        serviceCollection = new ServiceCollection();

        serviceCollection.AddSerializer<StartIDSerializer>();

        serviceCollection.AddStartGGClient().ConfigureHttpClient(c =>
        {
            c.BaseAddress = new Uri("https://api.start.gg/gql/alpha");
            c.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", Settings.GetSettings().StartGGToken);
        });

        serviceProvider = serviceCollection.BuildServiceProvider();
        client = serviceProvider.GetRequiredService<IStartGGClient>();
    }
}

public struct StartID
{
    public string String { get; }
    public int Number => this;

    public StartID(string s)
    {
        String = s;
    }

    public StartID(int i)
    {
        String = i.ToString();
    }

    public static implicit operator string(StartID d)
    {
        return d.String;
    }

    public static implicit operator int(StartID d)
    {
        if (int.TryParse(d.String, out var i))
        {
            return i;
        }

        throw new Exception("Failed to convert a StartID to an Int.");
    }

    public static implicit operator StartID(string d)
    {
        return new StartID(d);
    }

    public static implicit operator StartID(int i)
    {
        return new StartID(i);
    }

    public static bool operator ==(StartID lhs, StartID rhs)
    {
        return lhs.String == rhs.String;
    }

    public static bool operator !=(StartID lhs, StartID rhs)
    {
        return lhs.String != rhs.String;
    }

    public override string ToString()
    {
        return String;
    }
}

public class StartIDSerializer : ScalarSerializer<JsonElement, StartID>
{
    public StartIDSerializer()
        : base("StartID")
    {
    }

    public override StartID Parse(JsonElement serializedValue)
    {
        if (serializedValue.TryGetInt32(out var i))
        {
            return i;
        }

        Console.WriteLine(serializedValue.GetRawText());
        return 1;
    }

    protected override JsonElement Format(StartID runtimeValue)
    {
        // handle the serialization of the runtime representation in case
        // the scalar is used as a variable.
        var f = JsonSerializer.SerializeToElement(runtimeValue.String);
        return f;
    }
}