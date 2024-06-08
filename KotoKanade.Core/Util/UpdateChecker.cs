using System.Runtime.InteropServices;
using GithubReleaseDownloader;
using GithubReleaseDownloader.Entities;
using Mayerch1.GithubUpdateCheck;

namespace KotoKanade.Core.Util;

public sealed class UpdateChecker
{
	private readonly GithubUpdateCheck update;
	private readonly string username;
	private readonly string repository;
	private string? repoVersion;
	private Release? release;

	private UpdateChecker(
		string username = "InuInu2022",
		string repository = "KotoKanade"
	)
	{
		update = new GithubUpdateCheck(username, repository, CompareType.Boolean);
		this.username = username;
		this.repository = repository;
	}

	public static UpdateChecker Build()
	{
		return new UpdateChecker();
	}

	public async ValueTask<string>
	GetRepositoryVersionAsync(
		bool useCache = false
	)
	{
		if(useCache && repoVersion is not null)
		{
			return repoVersion;
		}

		release = await ReleaseManager.Instance
			.GetLatestAsync(username, repository)
			.ConfigureAwait(false);

		repoVersion = release?.TagName ?? "v0.0.0";
		return repoVersion;
	}

	/// <summary>
	/// ローカルバージョンがリポジトリの最新バージョンと比較して
	/// アップデートが利用可能かどうかを非同期で確認します。
	/// </summary>
	/// <returns>アップデートが利用可能の場合はtrue、利用不可の場合はfalse。</returns>
	public Task<bool>
	IsAvailableAsync() =>
		update.IsUpdateAvailableAsync(
			AppUtil.GetAppVer()
		);

	public async ValueTask<string> GetDownloadUrlAsync()
	{
		release = await ReleaseManager.Instance
			.GetLatestAsync(username, repository)
			.ConfigureAwait(false);

		return release?
			.Assets
			.First(a => a.Name.Contains(RuntimeInformation.RuntimeIdentifier, StringComparison.OrdinalIgnoreCase))
			.DownloadUrl
			?? "https://github.com/InuInu2022/KotoKanade/releases";
	}
}