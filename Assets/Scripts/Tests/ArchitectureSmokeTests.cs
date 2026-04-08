using NUnit.Framework;
using OrcFarm.Interaction;
using OrcFarm.Player;

namespace OrcFarm.Tests
{
    /// <summary>
    /// Compile-time and reflection smoke tests.
    /// Verify that the public contracts exist with the expected shape.
    /// These tests contain no gameplay logic.
    /// </summary>
    [TestFixture]
    internal sealed class ArchitectureSmokeTests
    {
        [Test]
        public void IInteractable_IsInterface()
        {
            Assert.IsTrue(typeof(IInteractable).IsInterface);
        }

        [Test]
        public void IInteractionService_IsInterface()
        {
            Assert.IsTrue(typeof(IInteractionService).IsInterface);
        }

        [Test]
        public void IPlayerInputProvider_IsInterface()
        {
            Assert.IsTrue(typeof(IPlayerInputProvider).IsInterface);
        }

        [Test]
        public void PlayerInputWrapper_ImplementsIPlayerInputProvider()
        {
            Assert.IsTrue(typeof(IPlayerInputProvider).IsAssignableFrom(typeof(PlayerInputWrapper)));
        }

        [Test]
        public void PlayerInputWrapper_ImplementsIDisposable()
        {
            Assert.IsTrue(typeof(System.IDisposable).IsAssignableFrom(typeof(PlayerInputWrapper)));
        }
    }
}
