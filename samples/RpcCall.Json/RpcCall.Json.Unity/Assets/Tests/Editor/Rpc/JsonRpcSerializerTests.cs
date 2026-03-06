using System;
using Game.Rpc.Contracts;
using ULinkRPC.Core;
using ULinkRPC.Serializer.Json;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;

namespace Tests.Editor.Rpc
{
    public class JsonRpcSerializerTests
    {
        [Test]
        public void JsonSerializer_RoundTrip_LoginRequest()
        {
            var serializer = new JsonRpcSerializer();
            var input = new LoginRequest
            {
                Account = "demo",
                Password = "secret"
            };

            var bytes = serializer.Serialize(input);
            var output = serializer.Deserialize<LoginRequest>(new ReadOnlyMemory<byte>(bytes));

            NUnitAssert.AreEqual(input.Account, output.Account);
            NUnitAssert.AreEqual(input.Password, output.Password);
        }

        [Test]
        public void JsonSerializer_RoundTrip_LoginReply()
        {
            var serializer = new JsonRpcSerializer();
            var input = new LoginReply
            {
                Code = 200,
                Token = "abc"
            };

            var bytes = serializer.Serialize(input);
            var output = serializer.Deserialize<LoginReply>(new ReadOnlyMemory<byte>(bytes));

            NUnitAssert.AreEqual(input.Code, output.Code);
            NUnitAssert.AreEqual(input.Token, output.Token);
        }
    }
}
