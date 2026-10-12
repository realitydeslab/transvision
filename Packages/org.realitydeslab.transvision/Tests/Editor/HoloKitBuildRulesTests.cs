using System.Linq;
using HoloKit.Editor;
using NUnit.Framework;
using UnityEditor;

namespace TransVision.Tests
{
    /// <summary>
    /// The HoloKit SDK runs these rules before every iOS build; a failing error rule stops the build.
    /// </summary>
    public class HoloKitBuildRulesTests
    {
        [Test]
        public void IosBuild_PassesEveryHoloKitErrorRule()
        {
            var failing = HoloKitProjectValidation.RulesFor(BuildTargetGroup.iOS)
                .Where(rule => rule.Error && rule.IsRuleEnabled() && !rule.CheckPredicate())
                .Select(rule => rule.Message)
                .ToList();
            Assert.That(failing, Is.Empty);
        }
    }
}
