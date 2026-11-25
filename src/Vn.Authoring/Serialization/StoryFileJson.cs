using System.Text.Json.Nodes;
using Vn.Authoring.Model;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Serialization;

/// <summary>StoryFile 하나와 그 파일이 소유하는 노드를 결정적으로 직렬화한다.</summary>
public static class StoryFileJson
{
    /// <summary><b>2</b> — 카드의 대본이 카드 JSON 안에 든다(2026-10-29 · 프로젝트 형식 4). 이 버전만 읽는다.</summary>
    public const int CurrentFormatVersion = 2;

    public const string FileExtension = ".vnstory.json";

    public static string Write(StoryFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        ValidateNodeIds(file);
        return JsonSupport.ToDeterministicText(WriteObject(file));
    }

    public static StoryFile Read(string json)
    {
        JsonObject root = JsonSupport.ParseObject(json, "StoryFile");
        int version = (int?)root["formatVersion"] ?? 0;

        if (version != CurrentFormatVersion)
        {
            throw new InvalidDataException(
                string.Format(Msg.File_StoryFileFormatVersionIsnT, version, CurrentFormatVersion));
        }

        return ReadObject(root);
    }

    internal static JsonObject WriteObject(StoryFile file)
    {
        var nodes = new JsonArray();
        foreach (StoryNode node in file.Nodes)
        {
            nodes.Add(StoryNodeJson.Write(node));
        }

        return new JsonObject
        {
            ["formatVersion"] = CurrentFormatVersion,
            ["fileId"] = file.Id,
            ["name"] = file.Name,
            ["nodes"] = nodes
        };
    }

    internal static StoryFile ReadObject(JsonObject root)
    {
        if (root["nodes"] is not JsonArray nodes)
        {
            throw new InvalidDataException(Msg.File_StoryFileHasNoNodesArray);
        }

        string id = JsonSupport.RequiredText(root["fileId"], Msg.File_FileIdOfStoryFile);
        string name = JsonSupport.RequiredText(root["name"], string.Format(Msg.File_NameOfStoryFile, id));
        var file = new StoryFile(id, name);
        HashSet<string> nodeIds = new(StringComparer.Ordinal);

        foreach (JsonObject nodeJson in JsonSupport.Objects(nodes, string.Format(Msg.File_NodesOfStoryFile, id)))
        {
            StoryNode node = StoryNodeJson.Read(nodeJson);

            if (!nodeIds.Add(node.Id))
            {
                throw new InvalidDataException(string.Format(Msg.File_NodeIdIsDuplicatedWithin, id, node.Id));
            }

            file.Nodes.Add(node);
        }

        return file;
    }

    private static void ValidateNodeIds(StoryFile file)
    {
        HashSet<string> nodeIds = new(StringComparer.Ordinal);
        foreach (StoryNode node in file.Nodes)
        {
            if (!nodeIds.Add(node.Id))
            {
                throw new InvalidDataException(
                    string.Format(Msg.File_NodeIdIsDuplicatedWithin, file.Id, node.Id));
            }
        }
    }
}
