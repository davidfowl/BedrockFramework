using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BedrockTransports
{
    public class AzureSignalREndPoint : EndPoint
    {
        private string _endpointPath = string.Empty;
        public Uri Uri { get; set; }

        public string AccessToken { get; set; }


        public AzureSignalREndPoint(string connectionString,
                                    string hubName,
                                    AzureSignalREndpointType type, bool isNewEndpoint)
        {
            var endpoint = "";
            var accessKey = "";
            string port = null;
            if (isNewEndpoint)
            {
                _endpointPath = "/endpoint";
            }
            foreach (var item in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var index = item.IndexOf('=');
                var key = item.Substring(0, index);
                var value = item.Substring(index + 1);

                if (key == "Endpoint")
                {
                    endpoint = value;
                }
                else if (key == "AccessKey")
                {
                    accessKey = value;
                }
                else if (key == "Port")
                {
                    port = ":" + value;
                }
            }

            if (type == AzureSignalREndpointType.Server)
            {
                var cid = Guid.NewGuid();
                Uri = new Uri($"{endpoint}{port}{_endpointPath}/server/?hub={hubName}&cid={cid}");
                AccessToken = GenerateServerAccessToken(accessKey, endpoint, hubName);
            }
            else if (type == AzureSignalREndpointType.Client)
            {
                Uri = new Uri($"{endpoint}{port}{_endpointPath}/client/?hub={hubName}");
                AccessToken = GenerateClientAccessToken(accessKey, endpoint, hubName);
            }
        }

        public override string ToString()
        {
            return Uri.ToString();
        }

        private string GenerateClientAccessToken(string signingKey, string endpoint, string hubName)
        {
            var jwtTokenHandler = new JwtSecurityTokenHandler();

            var audience = $"{endpoint}{_endpointPath}/client/?hub={hubName}";

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = jwtTokenHandler.CreateJwtSecurityToken(
                issuer: null,
                audience: audience,
                subject: new ClaimsIdentity(),
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials);
            return jwtTokenHandler.WriteToken(token);
        }

        private string GenerateServerAccessToken(string signingKey, string endpoint, string hubName)
        {
            var jwtTokenHandler = new JwtSecurityTokenHandler();

            var audience = $"{endpoint}{_endpointPath}/server/?hub={hubName}";

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = jwtTokenHandler.CreateJwtSecurityToken(
                issuer: null,
                audience: audience,
                subject: new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) }),
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials);
            return jwtTokenHandler.WriteToken(token);
        }
    }

}
