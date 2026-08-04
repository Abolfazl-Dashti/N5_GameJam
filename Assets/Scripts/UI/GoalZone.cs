using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GoalZone : MonoBehaviour
{
    [Header("VFX & UI")]
    public ParticleSystem goalParticle;
    public ParticleSystem goalParticle1;
    public TMP_Text scoreText;  // تکست UI که شمارنده رو نشون میده

    public void PlayGoalEffect()
    {
        if (goalParticle) goalParticle.Play();
        if (goalParticle1) goalParticle1.Play();
    }
    
    
    // لایه‌ای که فقط دیسک داره - حساسه، اشتباه بدی کار نمیکنه
    // public LayerMask Disc;

    // private int score = 0;

    // private void OnTriggerEnter(Collider other)
    // {
    //     // چک میکنیم ابجکت ورودی لایه disc داره یا نه
    //     if ((Disc.value & (1 << other.gameObject.layer)) == 0) return;
    //
    //     goalParticle.Play();
    //     goalParticle1.Play();
    //
    //     score++;
    //     scoreText.text = score.ToString();
    // }
}

