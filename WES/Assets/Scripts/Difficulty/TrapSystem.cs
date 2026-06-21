using UnityEngine;

// 덫(③ 능동 안전, 건물효과 §3) — 범위 내 몬스터에 피해를 주는 발동 로직.
// 서버 권위. 자원을 미리 소모해 위협을 예방하는 ③분기 실현(예측-베팅-후회).
// R1: 발동 피해 로직(재사용 코어)만 검증. 실제 덫 건물 프리팹·설치(BuildingPlacementWorker)·
// 발동 트리거(몬스터 근접 감지 컴포넌트)·표지(협동 정보 공유)는 R2~R4 콘텐츠(designer/level-design/client).
public static class TrapSystem
{
    // _center 반경 _radius 내 살아있는 몬스터 전부에 _damage. 적중 수 반환. 서버에서 호출.
    public static int TriggerTrapDamage(Vector3 _center, float _radius, int _damage)
    {
        MonsterBase[] monsters = Object.FindObjectsByType<MonsterBase>(FindObjectsSortMode.None);
        float radiusSqr = _radius * _radius;
        int hit = 0;

        for (int i = 0; i < monsters.Length; i++)
        {
            MonsterBase monster = monsters[i];
            if (monster == null || monster.IsDead)
                continue;

            if ((monster.transform.position - _center).sqrMagnitude > radiusSqr)
                continue;

            monster.TakeDamage(_damage, null); // 환경 발동 — 가해자 없음
            hit++;
        }
        return hit;
    }
}
