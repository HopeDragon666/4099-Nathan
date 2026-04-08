param(
    [string]$ProjectRoot = "c:\Users\Moddwyn\Unity Projects\Work\4099-Nathan",
    [int]$QuestionCount = 100,
    [int]$Seed = 40990407
)

$ErrorActionPreference = "Stop"

$csvPath = Join-Path $ProjectRoot "Assets\Personal\Data\Vocabs.csv"
$difficultyFiles = [ordered]@{
    Easy = Join-Path $ProjectRoot "Assets\Personal\Data\SATQuestions.Easy.json"
    Medium = Join-Path $ProjectRoot "Assets\Personal\Data\SATQuestions.Medium.json"
    Hard = Join-Path $ProjectRoot "Assets\Personal\Data\SATQuestions.Hard.json"
    Expert = Join-Path $ProjectRoot "Assets\Personal\Data\SATQuestions.Expert.json"
}

if (-not (Test-Path $csvPath)) {
    throw "Vocabulary CSV not found at path: $csvPath"
}

$rng = New-Object System.Random($Seed)

$stopWords = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)
@(
    "to", "the", "a", "an", "of", "and", "or", "in", "on", "for", "with", "by", "be", "is", "are", "that", "this", "as", "at",
    "into", "from", "etc", "it", "its", "than", "over", "under", "up", "down", "off", "not", "one", "two", "three"
) | ForEach-Object { [void]$stopWords.Add($_) }

function Normalize-Grouping {
    param(
        [string]$Grouping,
        [string]$Definition
    )

    $trimmed = if ($null -eq $Grouping) { "" } else { $Grouping.Trim() }

    if ([string]::IsNullOrWhiteSpace($trimmed)) {
        if ($Definition -match "^\s*To\s+") {
            return "Verb"
        }

        return "Noun"
    }

    switch -Regex ($trimmed.ToLowerInvariant()) {
        "^adv$" { return "Adverb" }
        "^adverb$" { return "Adverb" }
        "^adjective$" { return "Adjective" }
        "^verb$" { return "Verb" }
        "^noun$" { return "Noun" }
        default {
            $titleCase = [System.Globalization.CultureInfo]::InvariantCulture.TextInfo
            return $titleCase.ToTitleCase($trimmed.ToLowerInvariant())
        }
    }
}

function Get-Tokens {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return @()
    }

    $normalized = [regex]::Replace($Text.ToLowerInvariant(), "[^a-z0-9 ]", " ")
    $parts = $normalized.Split(" ", [System.StringSplitOptions]::RemoveEmptyEntries)
    $tokens = New-Object System.Collections.Generic.List[string]

    foreach ($part in $parts) {
        if ($part.Length -lt 3) {
            continue
        }

        if ($stopWords.Contains($part)) {
            continue
        }

        $tokens.Add($part)
    }

    return $tokens.ToArray()
}

function Get-Similarity {
    param(
        [string[]]$ATokens,
        [string[]]$BTokens,
        [string]$AWord,
        [string]$BWord
    )

    $jaccard = 0.0

    if (($ATokens.Count -gt 0) -and ($BTokens.Count -gt 0)) {
        $setA = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($token in $ATokens) { [void]$setA.Add($token) }

        $setB = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($token in $BTokens) { [void]$setB.Add($token) }

        $intersection = 0
        foreach ($token in $setA) {
            if ($setB.Contains($token)) {
                $intersection++
            }
        }

        $union = $setA.Count + $setB.Count - $intersection
        if ($union -gt 0) {
            $jaccard = $intersection / [double]$union
        }
    }

    $lengthDifference = [math]::Abs($AWord.Length - $BWord.Length)
    $lengthScore = 1.0 - ([math]::Min($lengthDifference, 12) / 12.0)

    $prefixScore = 0.0
    $prefixLength = [math]::Min(3, [math]::Min($AWord.Length, $BWord.Length))
    if ($prefixLength -gt 0) {
        $aPrefix = $AWord.Substring(0, $prefixLength).ToLowerInvariant()
        $bPrefix = $BWord.Substring(0, $prefixLength).ToLowerInvariant()
        if ($aPrefix -eq $bPrefix) {
            $prefixScore = 1.0
        }
    }

    return ($jaccard * 0.7) + ($lengthScore * 0.2) + ($prefixScore * 0.1)
}

function Contains-TopicToken {
    param(
        [string[]]$Tokens,
        [string[]]$Exact,
        [string[]]$Prefixes
    )

    foreach ($token in $Tokens) {
        if ($Exact -contains $token) {
            return $true
        }

        foreach ($prefix in $Prefixes) {
            if ($token.StartsWith($prefix)) {
                return $true
            }
        }
    }

    return $false
}

function Get-Topic {
    param($Entry)

    $tokens = @($Entry.Tokens)

    if (Contains-TopicToken -Tokens $tokens -Exact @("law", "court", "crime", "rights", "citizen", "statute", "trial", "justice", "jury", "judge") -Prefixes @("legal", "govern", "polic", "legisl", "indict")) {
        return "Civic"
    }

    if (Contains-TopicToken -Tokens $tokens -Exact @("music", "voice", "soprano", "choir", "melody", "rhythm", "song", "lyric", "harmony") -Prefixes @("harmon", "musical", "sing")) {
        return "Music"
    }

    if (Contains-TopicToken -Tokens $tokens -Exact @("science", "study", "biology", "medical", "disease", "atom", "molecule", "laboratory", "measure", "physics", "analysis") -Prefixes @("chem", "biolog", "medic", "physic", "analy")) {
        return "Science"
    }

    if (Contains-TopicToken -Tokens $tokens -Exact @("god", "church", "faith", "worship", "divine", "sacred") -Prefixes @("relig", "theolog")) {
        return "Faith"
    }

    if (Contains-TopicToken -Tokens $tokens -Exact @("war", "battle", "army", "military", "weapon", "troop", "siege", "campaign", "tactic") -Prefixes @("fort", "milit", "combat")) {
        return "Conflict"
    }

    if (Contains-TopicToken -Tokens $tokens -Exact @("money", "wealth", "price", "trade", "market", "value", "cost", "coin", "bank", "finance", "tax") -Prefixes @("econom", "financ", "invest")) {
        return "Economy"
    }

    if (Contains-TopicToken -Tokens $tokens -Exact @("plant", "tree", "animal", "horse", "bird", "fish", "forest", "river", "mountain", "valley", "moon", "star", "earth", "nature") -Prefixes @("botan", "zoolog", "astr", "geolog")) {
        return "Nature"
    }

    return "General"
}

function Build-Prompt {
    param($Entry)

    $topic = Get-Topic -Entry $Entry
    $templates = @()

    switch ($Entry.Grouping) {
        "Verb" {
            switch ($topic) {
                "Civic" {
                    $templates = @(
                        "Facing public pressure, the council chose to ______ the policy before the next vote.",
                        "During cross-examination, the attorney moved to ______ the witness's earlier claim.",
                        "To restore trust in city hall, officials agreed to ______ the process immediately."
                    )
                }
                "Science" {
                    $templates = @(
                        "Before publishing the results, the researchers had to ______ the data with additional trials.",
                        "To avoid contamination, the lab team decided to ______ each sample before storage.",
                        "When the first method failed, the engineer tried to ______ the design in a controlled test."
                    )
                }
                "Conflict" {
                    $templates = @(
                        "As the campaign shifted, commanders chose to ______ instead of repeating the old strategy.",
                        "Under heavy pressure, the unit had to ______ quickly to prevent further losses.",
                        "The historian argued that one order to ______ changed the entire battle."
                    )
                }
                default {
                    $templates = @(
                        "Rather than ignore the issue, the director chose to ______ before the deadline passed.",
                        "In her final draft, the editor decided to ______ the sentence so the argument stayed clear.",
                        "After several warnings, the committee voted to ______ the outdated rule.",
                        "During negotiations, both sides agreed to ______ before tensions rose again.",
                        "To solve the problem efficiently, the team had to ______ without waiting for more instructions.",
                        "When new evidence appeared, investigators moved to ______ right away."
                    )
                }
            }
        }
        "Adjective" {
            switch ($topic) {
                "Civic" {
                    $templates = @(
                        "The editorial described the mayor's response as ______, especially during the press conference.",
                        "In the debate, her ______ tone drew criticism from both parties.",
                        "Voters viewed the policy as ______ after reviewing the implementation details."
                    )
                }
                "Music" {
                    $templates = @(
                        "Reviewers praised the soloist's ______ style, saying it shaped the entire performance.",
                        "Even in the final movement, the ensemble stayed ______ and never rushed the phrasing.",
                        "The conductor wanted a more ______ sound to match the mood of the piece."
                    )
                }
                default {
                    $templates = @(
                        "She had a ______ manner, adapting to each social situation with little effort.",
                        "Even during tense meetings, his tone remained ______ and professional.",
                        "Critics called the proposal ______ and questioned whether it could succeed.",
                        "The team's approach seemed ______ at first, but the results proved otherwise.",
                        "Her ______ response surprised the room and shifted the conversation immediately.",
                        "Because the process felt increasingly ______, the group asked for a simpler plan."
                    )
                }
            }
        }
        "Adverb" {
            $templates = @(
                "When the interviewer challenged her claim, she replied ______ and changed the room's tone.",
                "He moved ______ through the hallway, trying not to draw attention.",
                "During the negotiation, the lead delegate spoke ______ to keep both sides engaged.",
                "Under pressure, the witness answered ______, avoiding unnecessary detail.",
                "The presenter paused, then continued ______ so the audience could follow each step."
            )
        }
        default {
            switch ($topic) {
                "Civic" {
                    $templates = @(
                        "In the hearing, the attorney argued that the dispute turned on a single ______.",
                        "The committee's final decision rested on one ______ that members could not ignore.",
                        "During deliberations, jurors kept returning to the same ______ in the testimony."
                    )
                }
                "Music" {
                    $templates = @(
                        "In rehearsal, the conductor highlighted the ______ that carried the melody.",
                        "The critic wrote that one ______ gave the performance its distinctive character.",
                        "By the second movement, the ensemble centered the arrangement around a ______."
                    )
                }
                "Science" {
                    $templates = @(
                        "Before beginning the experiment, students reviewed each ______ listed in the protocol.",
                        "The textbook introduced ______ as a key term for understanding the chapter.",
                        "In lab, the instructor asked everyone to identify the ______ before taking measurements."
                    )
                }
                "Faith" {
                    $templates = @(
                        "The speaker returned to one ______ as the moral center of the sermon.",
                        "In the passage, the author treats ______ as a guiding principle of belief.",
                        "The congregation discussed a ______ that shaped its traditions for generations."
                    )
                }
                "Conflict" {
                    $templates = @(
                        "Military historians still debate the ______ that changed the course of the campaign.",
                        "The commander viewed the ______ as the turning point of the operation.",
                        "In the archive, one ______ appears repeatedly in accounts of the battle."
                    )
                }
                "Economy" {
                    $templates = @(
                        "Analysts cited one ______ as the main reason prices shifted so abruptly.",
                        "The report focused on a ______ that influenced both wages and investment.",
                        "During the briefing, the minister called ______ the market's central concern."
                    )
                }
                "Nature" {
                    $templates = @(
                        "During the field survey, the class documented a ______ near the riverbank.",
                        "The naturalist's journal described a rare ______ discovered on the expedition.",
                        "At the preserve, visitors gathered to observe a ______ highlighted by the guide."
                    )
                }
                default {
                    $templates = @(
                        "By the end of the chapter, the narrator treated the ______ as a turning point.",
                        "In class discussion, the professor framed ______ as the key idea to remember.",
                        "The report identified a ______ that explained the sudden change in outcomes.",
                        "During the interview, she referred to one ______ that shaped her decision.",
                        "The museum label described the artifact as a ______ with unusual historical value.",
                        "As the argument developed, the author returned repeatedly to the same ______."
                    )
                }
            }
        }
    }

    return $templates[$rng.Next(0, $templates.Count)]
}

function Get-Bucket {
    param(
        [object[]]$SortedScores,
        [string]$Difficulty
    )

    $count = $SortedScores.Count
    if ($count -eq 0) {
        return @()
    }

    $start = 0
    $end = $count - 1

    switch ($Difficulty) {
        "Easy" {
            $start = [int][math]::Floor($count * 0.60)
            $end = $count - 1
        }
        "Medium" {
            $start = [int][math]::Floor($count * 0.30)
            $end = [int][math]::Floor($count * 0.70) - 1
        }
        "Hard" {
            $start = [int][math]::Floor($count * 0.12)
            $end = [int][math]::Floor($count * 0.35) - 1
        }
        "Expert" {
            $start = 0
            $end = [int][math]::Max([math]::Floor($count * 0.12) - 1, 2)
        }
    }

    $start = [math]::Max($start, 0)
    $end = [math]::Min($end, $count - 1)

    if ($start -gt $end) {
        $start = 0
        $end = [math]::Min($count - 1, 19)
    }

    return @($SortedScores[$start..$end])
}

$entries = New-Object System.Collections.Generic.List[object]
$lines = Get-Content -Path $csvPath -Encoding UTF8

foreach ($line in $lines) {
    $trimmed = $line.Trim()

    if ([string]::IsNullOrWhiteSpace($trimmed)) {
        continue
    }

    if ($trimmed -eq "Index|Word|Definition|Grouping") {
        continue
    }

    $parts = $trimmed -split "\|", 4
    if ($parts.Length -lt 4) {
        continue
    }

    $index = 0
    if (-not [int]::TryParse($parts[0].Trim(), [ref]$index)) {
        continue
    }

    $word = $parts[1].Trim()
    $definition = $parts[2].Trim()
    if ([string]::IsNullOrWhiteSpace($word) -or [string]::IsNullOrWhiteSpace($definition)) {
        continue
    }

    $grouping = Normalize-Grouping -Grouping $parts[3] -Definition $definition

    $entries.Add([pscustomobject]@{
        Index = $index
        Word = $word
        Definition = $definition
        Grouping = $grouping
        Tokens = Get-Tokens -Text $definition
    })
}

$seenWords = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)
$uniqueEntries = New-Object System.Collections.Generic.List[object]

foreach ($entry in ($entries | Sort-Object Index)) {
    if ($seenWords.Add($entry.Word)) {
        $uniqueEntries.Add($entry)
    }
}

if ($uniqueEntries.Count -lt $QuestionCount) {
    throw "Only $($uniqueEntries.Count) unique vocabulary entries are available, but $QuestionCount are required."
}

$selectedEntries = @($uniqueEntries | Sort-Object { $rng.Next() } | Select-Object -First $QuestionCount)

$difficultyCount = [int][math]::Floor($QuestionCount / 4)
$difficulties = New-Object System.Collections.Generic.List[string]
foreach ($tier in @("Easy", "Medium", "Hard", "Expert")) {
    for ($i = 0; $i -lt $difficultyCount; $i++) {
        $difficulties.Add($tier)
    }
}
while ($difficulties.Count -lt $QuestionCount) {
    $difficulties.Add("Expert")
}
$difficulties = @($difficulties | Sort-Object { $rng.Next() })

$questions = New-Object System.Collections.Generic.List[object]

for ($i = 0; $i -lt $selectedEntries.Count; $i++) {
    $answer = $selectedEntries[$i]
    $difficulty = $difficulties[$i]

    $sameGroup = @($uniqueEntries | Where-Object {
        $_.Word -ne $answer.Word -and $_.Grouping -eq $answer.Grouping
    })

    if ($sameGroup.Count -lt 3) {
        $sameGroup = @($uniqueEntries | Where-Object {
            $_.Word -ne $answer.Word
        })
    }

    $scored = foreach ($candidate in $sameGroup) {
        [pscustomobject]@{
            Entry = $candidate
            Score = Get-Similarity -ATokens $answer.Tokens -BTokens $candidate.Tokens -AWord $answer.Word -BWord $candidate.Word
        }
    }

    $sortedScores = @($scored | Sort-Object Score -Descending)
    $bucket = Get-Bucket -SortedScores $sortedScores -Difficulty $difficulty

    $pickedDistractors = New-Object System.Collections.Generic.List[object]
    $pickedWords = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($item in ($bucket | Sort-Object { $rng.Next() })) {
        if ($pickedDistractors.Count -ge 3) {
            break
        }

        if ($pickedWords.Add($item.Entry.Word)) {
            $pickedDistractors.Add($item.Entry)
        }
    }

    if ($pickedDistractors.Count -lt 3) {
        foreach ($item in ($sortedScores | Sort-Object { $rng.Next() })) {
            if ($pickedDistractors.Count -ge 3) {
                break
            }

            if ($pickedWords.Add($item.Entry.Word)) {
                $pickedDistractors.Add($item.Entry)
            }
        }
    }

    if ($pickedDistractors.Count -lt 3) {
        throw "Unable to pick 3 distractors for answer word '$($answer.Word)'."
    }

    $options = @($pickedDistractors | ForEach-Object { $_.Word }) + @($answer.Word)
    $options = @($options | Sort-Object { $rng.Next() })

    $correctOptionIndex = [array]::IndexOf($options, $answer.Word)
    if ($correctOptionIndex -lt 0) {
        throw "Failed to determine correct option index for '$($answer.Word)'."
    }

    $questions.Add([pscustomobject][ordered]@{
        id = ("Q{0:D4}" -f ($i + 1))
        answerWord = $answer.Word
        answerIndex = $answer.Index
        grouping = $answer.Grouping
        difficulty = $difficulty
        prompt = Build-Prompt -Entry $answer
        options = $options
        correctOptionIndex = $correctOptionIndex
        correctOptionLabel = @("A", "B", "C", "D")[$correctOptionIndex]
        sourceDefinition = $answer.Definition
    })
}

$generatedAtUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")

foreach ($difficultyName in $difficultyFiles.Keys) {
    $difficultyQuestions = @($questions | Where-Object { $_.difficulty -eq $difficultyName })
    $difficultyDataset = [ordered]@{
        version = 1
        seed = $Seed
        generatedAtUtc = $generatedAtUtc
        sourceVocabularyFile = "Assets/Personal/Data/Vocabs.csv"
        difficulty = $difficultyName
        totalQuestions = $difficultyQuestions.Count
        questions = $difficultyQuestions
    }

    $difficultyDataset | ConvertTo-Json -Depth 8 | Set-Content -Path $difficultyFiles[$difficultyName] -Encoding UTF8
}

$manifestPath = Join-Path $ProjectRoot "Assets\Personal\Data\SATQuestions.Manifest.json"
$manifest = [ordered]@{
    version = 1
    seed = $Seed
    generatedAtUtc = $generatedAtUtc
    sourceVocabularyFile = "Assets/Personal/Data/Vocabs.csv"
    totalQuestions = $questions.Count
    files = [ordered]@{
        Easy = "Assets/Personal/Data/SATQuestions.Easy.json"
        Medium = "Assets/Personal/Data/SATQuestions.Medium.json"
        Hard = "Assets/Personal/Data/SATQuestions.Hard.json"
        Expert = "Assets/Personal/Data/SATQuestions.Expert.json"
    }
}

$manifest | ConvertTo-Json -Depth 6 | Set-Content -Path $manifestPath -Encoding UTF8

$summary = [ordered]@{
    manifestPath = $manifestPath
    easyPath = $difficultyFiles.Easy
    mediumPath = $difficultyFiles.Medium
    hardPath = $difficultyFiles.Hard
    expertPath = $difficultyFiles.Expert
    totalQuestions = $questions.Count
    uniqueAnswerWords = @($questions.answerWord | Sort-Object -Unique).Count
    easy = @($questions | Where-Object { $_.difficulty -eq "Easy" }).Count
    medium = @($questions | Where-Object { $_.difficulty -eq "Medium" }).Count
    hard = @($questions | Where-Object { $_.difficulty -eq "Hard" }).Count
    expert = @($questions | Where-Object { $_.difficulty -eq "Expert" }).Count
    invalidOptionCounts = @($questions | Where-Object { @($_.options).Count -ne 4 }).Count
    invalidAnswerBindings = @($questions | Where-Object { $_.options[$_.correctOptionIndex] -ne $_.answerWord }).Count
}

$summary | ConvertTo-Json -Depth 4
