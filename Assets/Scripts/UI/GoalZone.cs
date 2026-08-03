using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GoalZone : MonoBehaviour
{
    // پارتیکل سیستمی که باید پلی بشه
    public ParticleSystem goalParticle;
    // پارتیکل سیستمی که باید پلی بشه
    public ParticleSystem goalParticle1;
    // تکست UI که شمارنده رو نشون میده
    public TMP_Text scoreText;

    // لایه‌ای که فقط دیسک داره - حساسه، اشتباه بدی کار نمیکنه
    public LayerMask Disc;

    private int score = 0;

    private void OnTriggerEnter(Collider other)
    {
        // چک میکنیم ابجکت ورودی لایه disc داره یا نه
        if ((Disc.value & (1 << other.gameObject.layer)) == 0) return;

        goalParticle.Play();
        goalParticle1.Play();

        score++;
        scoreText.text = score.ToString();
    }
}

