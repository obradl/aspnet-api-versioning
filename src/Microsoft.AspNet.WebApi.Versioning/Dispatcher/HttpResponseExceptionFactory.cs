﻿namespace Microsoft.Web.Http.Dispatcher
{
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Contracts;
    using System.Net.Http;
    using System.Web.Http;
    using System.Web.Http.Dispatcher;
    using System.Web.Http.Tracing;
    using Versioning;
    using static ApiVersion;
    using static System.Net.HttpStatusCode;

    internal sealed class HttpResponseExceptionFactory
    {
        private static readonly string ControllerSelectorCategory = typeof( IHttpControllerSelector ).FullName;
        private readonly HttpRequestMessage request;

        internal HttpResponseExceptionFactory( HttpRequestMessage request )
        {
            Contract.Requires( request != null );
            this.request = request;
        }

        private ITraceWriter TraceWriter => request.GetConfiguration().Services.GetTraceWriter() ?? NullTraceWriter.Instance;

        private ApiVersioningOptions Options => request.GetApiVersioningOptions();

        [SuppressMessage( "Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Created exception cannot be disposed. Handled by the caller." )]
        internal HttpResponseException NewNotFoundOrBadRequestException( ControllerSelectionResult conventionRouteResult, ControllerSelectionResult directRouteResult ) =>
            CreateBadRequest( conventionRouteResult, directRouteResult ) ?? CreateNotFound( conventionRouteResult );

        [SuppressMessage( "Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Created exception cannot be disposed. Handled by the caller." )]
        internal HttpResponseMessage CreateBadRequestResponse( ApiVersion requestedVersion ) =>
            requestedVersion == null ? CreateBadRequestForUnspecifiedApiVersionOrInvalidApiVersion() : CreateBadRequestForUnsupportedApiVersion( requestedVersion );

        [SuppressMessage( "Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Created exception cannot be disposed. Handled by the caller." )]
        internal HttpResponseException CreateBadRequest( ApiVersion requestedVersion ) => new HttpResponseException( CreateBadRequestResponse( requestedVersion ) );

        [SuppressMessage( "Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Created exception cannot be disposed. Handled by the caller." )]
        private HttpResponseException CreateBadRequest( ControllerSelectionResult conventionRouteResult, ControllerSelectionResult directRouteResult )
        {
            Contract.Requires( conventionRouteResult != null );

            var requestedVersion = default( ApiVersion );

            if ( conventionRouteResult.CouldMatchVersion )
            {
                requestedVersion = conventionRouteResult.RequestedVersion;
            }
            else if ( directRouteResult != null && directRouteResult.CouldMatchVersion )
            {
                requestedVersion = directRouteResult.RequestedVersion;
            }
            else
            {
                return null;
            }

            return CreateBadRequest( requestedVersion );
        }

        [SuppressMessage( "Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Created exception cannot be disposed. Handled by the caller." )]
        private HttpResponseMessage CreateBadRequestForUnspecifiedApiVersionOrInvalidApiVersion()
        {
            var requestedVersion = request.GetRawRequestedApiVersion();
            var parsedVersion = default( ApiVersion );
            var message = default( string );

            if ( string.IsNullOrEmpty( requestedVersion ) )
            {
                message = SR.ApiVersionUnspecified;
                TraceWriter.Info( request, ControllerSelectorCategory, message );
                return Options.CreateBadRequest( request, "ApiVersionUnspecified", message, messageDetail: null );
            }
            else if ( TryParse( requestedVersion, out parsedVersion ) )
            {
                return null;
            }

            message = SR.VersionedResourceNotSupported.FormatDefault( request.RequestUri, requestedVersion );
            var messageDetail = SR.VersionedControllerNameNotFound.FormatDefault( request.RequestUri, requestedVersion );

            TraceWriter.Info( request, ControllerSelectorCategory, message );

            return Options.CreateBadRequest( request, "InvalidApiVersion", message, messageDetail );
        }

        [SuppressMessage( "Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Created exception cannot be disposed. Handled by the caller." )]
        private HttpResponseMessage CreateBadRequestForUnsupportedApiVersion( ApiVersion requestedVersion )
        {
            Contract.Requires( requestedVersion != null );
            Contract.Ensures( Contract.Result<HttpResponseMessage>() != null );

            var message = SR.VersionedResourceNotSupported.FormatDefault( request.RequestUri, requestedVersion );
            var messageDetail = SR.VersionedControllerNameNotFound.FormatDefault( request.RequestUri, requestedVersion );

            TraceWriter.Info( request, ControllerSelectorCategory, message );

            return Options.CreateBadRequest( request, "UnsupportedApiVersion", message, messageDetail );
        }

        [SuppressMessage( "Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Created exception cannot be disposed. Handled by the caller." )]
        private HttpResponseException CreateNotFound( ControllerSelectionResult conventionRouteResult )
        {
            Contract.Requires( conventionRouteResult != null );

            var message = SR.ResourceNotFound.FormatDefault( request.RequestUri );
            var messageDetail = default( string );

            if ( string.IsNullOrEmpty( conventionRouteResult.ControllerName ) )
            {
                messageDetail = SR.ControllerNameNotFound.FormatDefault( request.RequestUri );
            }
            else
            {
                messageDetail = SR.DefaultControllerFactory_ControllerNameNotFound.FormatDefault( conventionRouteResult.ControllerName );
            }

            TraceWriter.Info( request, ControllerSelectorCategory, message );

            return new HttpResponseException( request.CreateErrorResponse( NotFound, message, messageDetail ) );
        }
    }
}