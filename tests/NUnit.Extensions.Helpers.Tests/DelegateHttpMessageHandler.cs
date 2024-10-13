namespace NUnit.Extensions.Helpers.Tests;

public class DelegateHttpMessageHandler : HttpMessageHandler
{
	public Func<HttpRequestMessage, HttpResponseMessage>? RequestDelegate { get; set; }

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var response = RequestDelegate?.Invoke(request)!;
		cancellationToken.ThrowIfCancellationRequested();

		return Task.FromResult(response);
	}
}

