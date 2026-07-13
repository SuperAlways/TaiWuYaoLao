using System.Threading;
using System.Threading.Tasks;

namespace TaiwuEncyclopedia.Core.Rag;

/// <summary>
/// RAG 检索客户端接口。Core 定义契约，Frontend 层用 UnityWebRequest 实现。
/// RetrieveRagTool 通过此接口解耦 HTTP 传输。
/// </summary>
public interface IRagClient
{
    Task<RagRetrieveResult> RetrieveAsync(RagRetrieveRequest request, CancellationToken ct = default);
}
