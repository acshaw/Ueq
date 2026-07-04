# Trellis Region — Zone Design Document
## MVP Starting Zones

---

## Overview

Three connected zones forming the MVP starting experience. Trellis village is not a standalone zone — it is a settlement cluster within the first zone. Players begin in Creslin's Field, graduate into Thornwood, and eventually enter Grukmar's Deep.

---

## Zone 1: Creslin's Field

### Geography
- Large open field zone
- Western boundary: ocean coastline
- Eastern boundary: hills rising toward impassable mountain range
- Northern boundary: treeline marking Thornwood entrance
- Southern boundary: road leading south toward Traveler's Rest

### Settlement: Trellis Village
- Small cluster of buildings near the center-west of the zone
- Agrarian, isolated, generational community
- Residents aware something is wrong but haven't named it precisely
- Cultural taboo around the northern treeline — enforced by fear, not law

### Zone Population
- Fields: wolves, wildlife, low level human bandits on the southern road
- Hills toward mountains: harder wildlife, early goblin scouts crossing over from Thornwood
- Road: travelers, merchants, occasional bandit camp

### Difficulty Gradient
- Village immediate area: levels 1-3
- Open fields: levels 2-5
- Eastern hills: levels 4-7
- Northern field edge near treeline: levels 6-8

### Key NPCs

**Trellis Village**

- **Innkeeper** — functional vendor, rumors about strange noises at night, keyword: *night*, *scratching*, *forest*
- **Blacksmith** — weapons and armor vendor, keyword: *goblins* unlocks if player has spoken to the veteran first
- **Elder** — town authority, dismissive of danger, keyword: *forest* produces denial, keyword: *veteran* points to Aldric
- **Children (non-combat NPC cluster)** — ambient dialogue about daring each other to touch the treeline, keyword: *dare*, *boundary*, *monster*
- **Aldric** — the veteran. Central information NPC. See NPC detail below.

### Aldric — Veteran NPC Detail
- Former soldier, has lived in Trellis 20 years
- Went into Thornwood once, years ago, came back alone
- Has been quietly monitoring goblin activity since
- Knows the dungeon exists, knows it has a name among the goblins, does not know the Warchief's name
- Watches the village perimeter at night, has killed goblins in the village three times in the past year
- Keywords: *forest*, *goblins*, *night*, *scratching*, *danger*, *dungeon*, *deep*, *warchief* (partial — he knows something rules them, not the name)
- Group hail: if party contains a ranger class, Aldric provides patrol route information for Thornwood. If party contains a warrior, he describes the dungeon entrance location specifically.

### World-State Notes
- Goblin night raids into Trellis are an escalating event
- Early in server life: villagers report scratching sounds, one missing chicken
- As Grukmar's Deep population goes unthinned: actual goblin incursions into village at night become encounters
- Guild quest boards in later content can reference Trellis goblin pressure as an active bounty source

---

## Zone 2: Thornwood

### Geography
- Dense forest zone
- Southern boundary: treeline bordering Creslin's Field (zone transition)
- Eastern boundary: mountain range face — impassable, vertical cliff geography
- Western boundary: coastal cliffs, no beach access
- Northern boundary: deep forest, zone edge
- Grukmar's Deep entrance: carved into the mountain face, eastern boundary, mid-north position

### Tone
- Darker than Creslin's Field, canopy blocks light
- Patrol-heavy — goblins actively move through the forest in organized groups
- Wildlife is hostile and territorial
- No settlements, no safe areas

### Zone Population
- Forest wildlife: wolves, bears, territorial predators
- Goblin patrols: mixed Goblin Male and Female scouts moving in groups of 2-4
- Goblin Warrior Male and Female: closer to the dungeon entrance, organized patrol routes
- Named patrol leader: optional rare spawn, carries dungeon-relevant loot or keyword information

### Patrol and Spawn Design
- Variable spawn timers on all goblin groups
- Two to three patrol routes with randomized waypoint timing
- Zone sweeper: a large predator (bear or wolf pack) that moves unpredictably through the forest and can disrupt goblin patrols and players simultaneously
- Goblin patrols near dungeon entrance are larger and more organized than field-edge patrols

### Difficulty Gradient
- Southern treeline edge: levels 6-9
- Mid forest: levels 8-12
- Northern deep forest: levels 10-14
- Dungeon entrance approach: levels 12-15

### Key Notes
- No NPC vendors or safe rest points inside Thornwood
- Players who die deep in Thornwood have a meaningful corpse run
- The dungeon entrance is not immediately visible from the zone transition — players have to move through forest to find it
- Goblin patrol behavior near the entrance should communicate that something organized is inside

---

## Zone 3: Grukmar's Deep

### Geography
- Underground dungeon carved into the mountain face
- Single entrance from Thornwood
- Expands downward and inward — deeper means harder
- Mountain stone aesthetic, rough-carved in upper levels, more structured in lower levels suggesting older construction

### Lore
- Goblins have occupied this site for at least two generations
- The Warchief has consolidated multiple goblin clans under one hierarchy in recent years — this is what is driving the increased boldness and village raids
- The dungeon has a name among the goblins: Grukmar's Deep — Grukmar was a previous Warchief whose name became the location's identity
- Current Warchief's name: TBD — to be named during content authoring

### Population and Hierarchy

| Mob | Location | Role | Notes |
|---|---|---|---|
| Goblin Male / Female | Entrance level | Scouts, foragers | Lowest difficulty, disorganized |
| Goblin Warrior Male / Female | Mid dungeon | Military caste | Patrol in pairs, organized |
| Goblin Shaman | Mid to deep | Support caste | Buffs nearby goblins, has spells, changes group strategy |
| Goblin Warchief | Deepest level | Boss | Named encounter, political center of dungeon |

### Difficulty Gradient
- Entrance level: levels 8-12
- Mid dungeon: levels 11-15
- Deep dungeon: levels 14-18
- Warchief chamber: levels 17-20

### CC and HO Design Notes
- Goblin Shaman is the primary CC justification — groups that ignore him in favor of warriors will struggle
- Shaman buffs create a priority kill order naturally without explicit instruction
- Warchief encounter should require sustained group coordination — area of control from warrior, CC on adds, sustained healing pressure
- Environmental chokepoints at mid-dungeon transition — corridor geometry that rewards positioning

### Key Named Encounters
- **Warchief** (boss, deepest level) — world-state kill contributes to reducing Trellis night raid frequency
- **Shaman Council** (mid dungeon, rare spawn) — three shamans together, significant challenge, notable loot
- **Gate Captain** (entrance level named) — warrior variant, guards dungeon entrance, intro named encounter

---

## Zone Connections

```
[Ocean] — Creslin's Field — [Road to Traveler's Rest]
              |
         Trellis Village
              |
         [Treeline]
              |
          Thornwood
              |
      [Mountain Face]
              |
        Grukmar's Deep
```

---

## Content Authoring Priority

1. Trellis village NPC conversations and keyword trees
2. Creslin's Field spawn tables and patrol paths
3. Aldric keyword conversation depth
4. Thornwood goblin patrol routes and spawn variance
5. Grukmar's Deep entrance level mob population
6. Mid dungeon population and Shaman behavior
7. Named encounters
8. Warchief encounter

---

## Open Questions

- Warchief name and lore detail
- Specific loot table design
- World-state escalation thresholds for Trellis night raids
- Thornwood zone sweeper species
- Goblin patrol leader name and keyword drop content
- Whether Aldric becomes a quest giver or remains purely informational
