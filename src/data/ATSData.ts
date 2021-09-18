import * as _ from 'lodash';
import { IAchievement, ITutorial, IScore } from '../services/ATSManager';

// Achievement Tutorial Score Datas

// enum AchievementId {
//   SOLDIER_BRONZE, SOLDIER_SILVER, SOLDIER_GOLD,
//   CONQUEROR_BRONZE, CONQUEROR_SILVER, CONQUEROR_GOLD,
//   DEFENDER_BRONZE, DEFENDER_SILVER, DEFENDER_GOLD,
//   PERFECTION_BRONZE, PERFECTION_SILVER, PERFECTION_GOLD,
//   RIDDLER_BRONZE, RIDDLER_SILVER, RIDDLER_GOLD,
//   EXPLORER_BRONZE, EXPLORER_SILVER, EXPLORER_GOLD,
// }

// enum ScoreId {
//   KILL_ENEMY,
//   PLAYER_DEATH,
// }

export function genAchievements() {
  let ACHIEVEMENTS: IAchievement[] = [
    // {
    //   id: AchievementId.SOLDIER_BRONZE,
    //   title: 'Bronze Soldier',
    //   caption: 'Destroy 10 enemy ships!',
    //   emitter: GameEvents.NOTIFY_ENEMY_DESTROYED,
    //   condition: extrinsic => extrinsic.data.scores.kills >= 10,
    // },
    // {
    //   id: AchievementId.SOLDIER_SILVER,
    //   prev: AchievementId.SOLDIER_BRONZE,
    //   title: 'Silver Soldier',
    //   caption: 'Destroy 100 enemy ships!',
    //   emitter: GameEvents.NOTIFY_ENEMY_DESTROYED,
    //   condition: extrinsic => extrinsic.data.scores.kills >= 100,
    // },
    // {
    //   id: AchievementId.SOLDIER_GOLD,
    //   prev: AchievementId.SOLDIER_SILVER,
    //   title: 'Gold Soldier',
    //   caption: 'Destroy 1000 enemy ships!',
    //   emitter: GameEvents.NOTIFY_ENEMY_DESTROYED,
    //   condition: extrinsic => extrinsic.data.scores.kills >= 1000,
    // },
    // {
    //   id: AchievementId.CONQUEROR_BRONZE,
    //   title: 'Bronze Conqueror',
    //   caption: 'Complete level 1',
    //   emitter: GameEvents.NOTIFY_LEVEL_COMPLETED,
    //   condition: (extrinsic, e: ILevelEvent) => (e.win && e.levelIndex >= 0),
    // },
    // {
    //   id: AchievementId.CONQUEROR_SILVER,
    //   prev: AchievementId.CONQUEROR_BRONZE,
    //   title: 'Silver Conqueror',
    //   caption: 'Complete level 6',
    //   emitter: GameEvents.NOTIFY_LEVEL_COMPLETED,
    //   condition: (extrinsic, e: ILevelEvent) => (e.win && e.levelIndex >= 5),
    // },
    // {
    //   id: AchievementId.CONQUEROR_GOLD,
    //   prev: AchievementId.CONQUEROR_SILVER,
    //   title: 'Gold Conqueror',
    //   caption: 'Complete the game',
    //   emitter: GameEvents.NOTIFY_LEVEL_COMPLETED,
    //   condition: (extrinsic, e: ILevelEvent) => (e.win && e.levelIndex >= 11),
    // },
    // {
    //   id: AchievementId.DEFENDER_BRONZE,
    //   title: 'Bronze Defender',
    //   caption: 'Complete any level with perfect health',
    //   emitter: GameEvents.NOTIFY_LEVEL_COMPLETED,
    //   condition: (extrinsic, e: ILevelEvent) => (e.win && !e.levelInstance.healthLost),
    // },
    // {
    //   id: AchievementId.DEFENDER_SILVER,
    //   prev: AchievementId.DEFENDER_BRONZE,
    //   title: 'Silver Defender',
    //   caption: 'Complete 3 levels with perfect health',
    //   emitter: GameEvents.NOTIFY_LEVEL_COMPLETED,
    //   condition: (extrinsic, e: ILevelEvent) => (_.filter(extrinsic.data.levels, {healthBadge: 3}).length >= 3),
    // },
    // {
    //   id: AchievementId.DEFENDER_GOLD,
    //   prev: AchievementId.DEFENDER_SILVER,
    //   title: 'Gold Defender',
    //   caption: 'Completed 10 levels with perfect health',
    //   emitter: GameEvents.NOTIFY_LEVEL_COMPLETED,
    //   condition: (extrinsic, e: ILevelEvent) => (_.filter(extrinsic.data.levels, {healthBadge: 3}).length >= 10),
    // },
    // {
    //   id: AchievementId.PERFECTION_BRONZE,
    //   title: 'Bronze Perfection',
    //   caption: 'Complete any level with perfect score',
    //   emitter: GameEvents.NOTIFY_LEVEL_COMPLETED,
    //   condition: (extrinsic, e: ILevelEvent) => (e.win && e.levelInstance.enemiesKilled >= e.levelInstance.totalEnemies),
    // },
    // {
    //   id: AchievementId.PERFECTION_SILVER,
    //   prev: AchievementId.PERFECTION_BRONZE,
    //   title: 'Silver Perfection',
    //   caption: 'Complete 3 levels with perfect score',
    //   emitter: GameEvents.NOTIFY_LEVEL_COMPLETED,
    //   condition: (extrinsic, e: ILevelEvent) => (_.filter(extrinsic.data.levels, {killBadge: 3}).length >= 3),
    // },
    // {
    //   id: AchievementId.PERFECTION_GOLD,
    //   prev: AchievementId.PERFECTION_SILVER,
    //   title: 'Gold Perfection',
    //   caption: 'Complete 10 levels with perfect score',
    //   emitter: GameEvents.NOTIFY_LEVEL_COMPLETED,
    //   condition: (extrinsic, e: ILevelEvent) => (_.filter(extrinsic.data.levels, {killBadge: 3}).length >= 10),
    // },
    // {
    //   id: AchievementId.RIDDLER_BRONZE,
    //   title: 'Bronze Riddler',
    //   caption: "It's a bird, it's a plane, it's...",
    //   emitter: GameEvents.NOTIFY_WORD_COMPLETED,
    //   condition: (extrinsic, e: IWordEvent) => e.word === 'superman',
    // },
    // {
    //   id: AchievementId.RIDDLER_SILVER,
    //   prev: AchievementId.RIDDLER_BRONZE,
    //   title: 'Silver Riddler',
    //   caption: "Boy George's Karma ...",
    //   emitter: GameEvents.NOTIFY_WORD_COMPLETED,
    //   condition: (extrinsic, e: IWordEvent) => e.word === 'chameleon',
    // },
    // {
    //   id: AchievementId.RIDDLER_GOLD,
    //   prev: AchievementId.RIDDLER_SILVER,
    //   title: 'Gold Riddler',
    //   caption: "Fighter Pilot's Suicide",
    //   emitter: GameEvents.NOTIFY_WORD_COMPLETED,
    //   condition: (extrinsic, e: IWordEvent) => e.word === 'kamikaze',
    // },
    // {
    //   id: AchievementId.EXPLORER_BRONZE,
    //   title: 'Bronze Explorer',
    //   caption: 'Complete the Typing Test',
    //   emitter: GameEvents.NOTIFY_TEST_COMPLETE,
    //   condition: (extrinsic, e: ITestEvent) => true,
    // },
    // {
    //   id: AchievementId.EXPLORER_SILVER,
    //   title: 'Silver Explorer',
    //   caption: 'Pause the game without clicking',
    //   emitter: GameEvents.NOTIFY_WORD_COMPLETED,
    //   condition: (extrinsic, e: IWordEvent) => e.word === 'pause',
    // },
    // {
    //   id: AchievementId.EXPLORER_GOLD,
    //   title: 'Gold Explorer',
    //   caption: 'See who made the game',
    //   emitter: GameEvents.NOTIFY_CREDITS_VIEWED,
    //   condition: (extrinsic, e: null) => true,
    // },
  ];
  return ACHIEVEMENTS;
}

export function genTutorials() {
  let m: ITutorial[] = [];

  return m;
}

export function genScores() {
  let m: IScore[] = [
    // {
    //   id: ScoreId.KILL_ENEMY,
    //   type: '++',
    //   prop: 'scores.kills',
    //   emitter: GameEvents.NOTIFY_ENEMY_DESTROYED,
    //   condition: () => true,
    // },
    // {
    //   id: ScoreId.PLAYER_DEATH,
    //   type: '++',
    //   prop: 'scores.deaths',
    //   emitter: GameEvents.NOTIFY_LEVEL_COMPLETED,
    //   condition: (extrinsic: ExtrinsicModel, e: ILevelEvent) => !e.win,
    // },
  ];

  return m;
}
