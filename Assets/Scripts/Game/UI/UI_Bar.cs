using UnityEngine;
using UnityEngine.UI;

public class UI_Bar : MonoBehaviour
{
    private Image curBar = null;
    private Image maxBar = null;

    private float maxDrawPercentage = 1.0f;
    private float curPercentage = 0;

    private bool canOverflow = false;

    private bool manipulateX = true;
    private bool manipulateY = false;

    void Awake()
    {
        curBar = GetComponent<Image>();

        maxBar = transform.parent.GetComponent<Image>();
    }

    void Update()
    {
        
    }

    public void SetPercentage(float percentage) // 0 - 1
    {
        curPercentage = percentage;

        RecalculateBar();
    }

    private void RecalculateBar()
    {
        if (!curBar || !maxBar)
            return;

        if(!canOverflow)
        {
            if(curPercentage > maxDrawPercentage)
                curPercentage = maxDrawPercentage;
            else
                if(curPercentage < 0.0f)
                curPercentage = 0.0f;
        }

        float currentPercentage = curPercentage / maxDrawPercentage;

        var size = curBar.rectTransform.rect;
        var orgSize = maxBar.rectTransform.rect;

        float deltaX = 0.0f;
        float deltaY = 0.0f;

        if (manipulateX)
        {
            float nextWidth = orgSize.width * currentPercentage;

            deltaX = (nextWidth - size.width) / 2;

            size.width = nextWidth;
        }

        if (manipulateY)
        {
            float nextHeight = orgSize.height * currentPercentage;

            size.height = nextHeight;
        }

        curBar.rectTransform.sizeDelta = new Vector2(size.width, size.height);
        curBar.rectTransform.anchoredPosition = new Vector3((size.width - orgSize.width) / 2, (size.height - orgSize.height) / 2);
    }
}
