using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// Game > Generate Animation Data Assets
///
/// 9개 직업의 PlayerAnimationData .asset 파일을
/// Assets/Scripts/Player/AnimationData/ 폴더에 자동 생성합니다.
///
/// ※ 이미 파일이 있으면 덮어쓰지 않고 클립만 갱신합니다.
///    나중에 클립을 교체하고 싶으면:
///    - Game > Player Animation Setup 창에서 드롭다운으로 교체
///    - 또는 Project 창에서 .asset 파일 선택 후 Inspector에서 직접 변경
/// </summary>
public static class PlayerAnimationDataGenerator
{
    private const string SaveFolder = "Assets/Scripts/Player/AnimationData";

    [MenuItem("Game/Generate Animation Data Assets")]
    public static void Generate()
    {
        // 폴더 없으면 생성
        if (!AssetDatabase.IsValidFolder(SaveFolder))
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath,
                "Scripts/Player/AnimationData"));
            AssetDatabase.Refresh();
        }

        var jobs = (PlayerJob[])System.Enum.GetValues(typeof(PlayerJob));
        int created = 0, updated = 0;

        foreach (var job in jobs)
        {
            string path = $"{SaveFolder}/Anim_{job}.asset";
            var paths = PlayerAnimationData.GetDefaultPaths(job);

            // 기존 에셋 로드 or 새로 생성
            var data = AssetDatabase.LoadAssetAtPath<PlayerAnimationData>(path);
            bool isNew = data == null;
            if (isNew)
            {
                data = ScriptableObject.CreateInstance<PlayerAnimationData>();
                data.job = job;
            }

            // 클립 할당 (기존 에셋이면 비어 있는 슬롯만 채움)
            data.armedIdle   = Pick(data.armedIdle,   paths.armedIdle);
            data.armedWalk   = Pick(data.armedWalk,   paths.armedWalk);
            data.armedRun    = Pick(data.armedRun,    paths.armedRun);
            data.unarmedIdle = Pick(data.unarmedIdle, paths.unarmedIdle);
            data.unarmedWalk = Pick(data.unarmedWalk, paths.unarmedWalk);
            data.unarmedRun  = Pick(data.unarmedRun,  paths.unarmedRun);
            data.jump        = Pick(data.jump,         paths.jump);
            data.hit         = Pick(data.hit,          paths.hit);
            data.death       = Pick(data.death,        paths.death);

            if (isNew)
            {
                AssetDatabase.CreateAsset(data, path);
                created++;
            }
            else
            {
                EditorUtility.SetDirty(data);
                updated++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 생성된 폴더를 Project 창에서 강조
        var folder = AssetDatabase.LoadAssetAtPath<Object>(SaveFolder);
        EditorGUIUtility.PingObject(folder);

        Debug.Log($"[AnimGenerator] 완료 — 새로 생성: {created}개 / 기존 갱신: {updated}개\n" +
                  $"저장 위치: {SaveFolder}");

        EditorUtility.DisplayDialog(
            "Animation Data 생성 완료",
            $"새로 생성: {created}개\n기존 갱신: {updated}개\n\n저장 위치:\n{SaveFolder}\n\n" +
            "클립을 교체하려면:\n" +
            "• Game > Player Animation Setup 창 사용\n" +
            "• 또는 Project 창에서 .asset 파일 선택 후 Inspector에서 변경",
            "확인");
    }

    /// <summary>
    /// 기존 에셋에 이미 클립이 있으면 그대로 유지,
    /// 비어 있으면 기본 경로에서 로드해 채웁니다.
    /// </summary>
    private static AnimationClip Pick(AnimationClip existing, string defaultPath)
    {
        if (existing != null) return existing;
        if (string.IsNullOrEmpty(defaultPath)) return null;

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(defaultPath);
        if (clip == null)
            Debug.LogWarning($"[AnimGenerator] 클립을 찾을 수 없음: {defaultPath}");
        return clip;
    }
}
