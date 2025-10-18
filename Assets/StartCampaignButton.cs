using UnityEngine;

public class CampaignStartButton : MonoBehaviour
{
    public CampaignManager campaign;

    public void StartCampaign()   // <--- THIS must exist here!
    {
        if (campaign == null)
            campaign = FindObjectOfType<CampaignManager>();

        if (campaign != null)
            campaign.StartCampaign();
    }
}
