using System.Collections.Generic;
using LucidSightTools;
using TMPro;
using UnityEngine;

public class Scoreboard : MonoBehaviour
{
    [SerializeField]
    private GameObject scoreEntryPrefab = null;

    [SerializeField]
    private Transform scoreRoot = null;
    private List<ScoreboardEntry> spawnedEntries = new List<ScoreboardEntry>();

    public void CreateScoreEntry(ArenaNetworkedEntity entity)
    {
        GameObject newEntry = Instantiate(scoreEntryPrefab, scoreRoot, false);
        ScoreboardEntry entry = newEntry.GetComponent<ScoreboardEntry>();
        entry.Init(entity);
        spawnedEntries.Add(entry);
    }
    public void UpdateScore(ArenaScoreUpdateMessage updateMessage)
    {
        ScoreboardEntry entryForView = GetEntryByID(updateMessage.entityID);
        if (entryForView != null)
        {
            entryForView.UpdateScore(updateMessage.score);
            UpdateEntryOrder();
        }
        else
        {
            LSLog.LogError("Tried to Update a score but couldn't find an entry!");
        }

    }
    public void RemoveView(ArenaNetworkedEntity entity)
    {
        ScoreboardEntry entryForView = GetEntryByID(entity.id);

        if (entryForView != null)
        {
            spawnedEntries.Remove(entryForView);
            Destroy(entryForView.gameObject);
        }
        else
        {
            LSLog.LogError("Player left game but we do not have a scoreboard entry for them!");
        }
    }

    private ScoreboardEntry GetEntryByID(string entityID)
    {
        ScoreboardEntry entryForView = null;
        foreach (ScoreboardEntry score in spawnedEntries)
        {
            if (score.entityRef.id.Equals(entityID))
            {
                entryForView = score;
            }
        }

        return entryForView;
    }

    public void ResetScores()
    {
        foreach (ScoreboardEntry score in spawnedEntries)
        {
            score.UpdateScore(0);
        }
    }


    private void UpdateEntryOrder()
    {
        spawnedEntries.Sort((x, y) =>
        {
            int scoreX = x != null ? x.currentScore : -1;
            int scoreY = y != null ? y.currentScore : -1;

            return scoreY.CompareTo(scoreX);
        });

        for (int i = 0; i < spawnedEntries.Count; ++i)
        {
            spawnedEntries[i].transform.SetSiblingIndex(i);
        }
    }
}