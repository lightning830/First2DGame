using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class CrystalRushSceneTests
{
    [UnityTest]
    public IEnumerator SampleScene_LoadsAndStartsARound()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;

        var gameRoot = GameObject.Find("GameRoot");
        Assert.That(gameRoot, Is.Not.Null, "GameRoot should provide the scene-owned game controller.");
        Assert.That(GameObject.Find("World"), Is.Not.Null);
        Assert.That(GameObject.Find("Spawns"), Is.Not.Null);
        Assert.That(GameObject.Find("UI"), Is.Not.Null);

        gameRoot.SendMessage("BeginRound", SendMessageOptions.RequireReceiver);
        yield return null;

        var actors = GameObject.Find("RuntimeActors");
        Assert.That(actors, Is.Not.Null);
        Assert.That(actors.transform.childCount, Is.EqualTo(12), "One player, one hunter, and ten crystal prefabs should be spawned.");
    }
}
