const pluginId = '217e21a6-08a7-4495-be64-a3cd6a7fa200';

const defaultBadges = [
    { Text: 'VO', ColorHex: '#C4A35A', TextColorHex: '#F6F1E7', SizePercent: 18, Markers: '', Filled: false, Enabled: true },
    { Text: 'VF', ColorHex: '#1F7A4D', TextColorHex: '#F7F8FA', SizePercent: 18, Markers: 'vf,vff,vfi,fr-fr,fra-fr,fre-fr,truefrench,true french,fra,fre,fr,french,français,francais', Filled: true, Enabled: true }
];

function upgrade(root) {
    if (!window.CustomElements) {
        return;
    }

    root.querySelectorAll('[is]').forEach((element) => {
        try {
            window.CustomElements.upgrade(element);
        } catch (ex) {
            console.error(ex);
        }
    });
}

function jellyfinInput(className, type, value) {
    const input = document.createElement('input');
    input.setAttribute('is', 'emby-input');
    input.type = type;
    input.className = className;
    if (type === 'number') {
        input.min = '5';
        input.max = '50';
    }
    input.value = value ?? '';
    return input;
}

function jellyfinField(label, input, hint) {
    const wrap = document.createElement('div');
    wrap.className = 'inputContainer';
    const lab = document.createElement('label');
    lab.className = 'inputLabel inputLabelUnfocused';
    lab.textContent = label;
    wrap.append(lab, input);
    if (hint) {
        const desc = document.createElement('div');
        desc.className = 'fieldDescription';
        desc.textContent = hint;
        wrap.append(desc);
    }
    return wrap;
}

function jellyfinCheck(className, checked, label) {
    const wrap = document.createElement('div');
    wrap.className = 'checkboxContainer checkboxContainer-withDescription';
    const lab = document.createElement('label');
    lab.className = 'emby-checkbox-label';
    const input = document.createElement('input');
    input.setAttribute('is', 'emby-checkbox');
    input.type = 'checkbox';
    input.className = className;
    input.checked = !!checked;
    const span = document.createElement('span');
    span.textContent = label;
    lab.append(input, span);
    wrap.append(lab);
    return wrap;
}

function badgeRow(badge) {
    const row = document.createElement('div');
    row.className = 'badge-row';
    const head = document.createElement('div');
    head.className = 'badge-head';
    head.style.cssText = 'display:flex;align-items:center;gap:.6em;margin-bottom:.8em;font-weight:600;';
    const remove = document.createElement('button');
    remove.setAttribute('is', 'emby-button');
    remove.type = 'button';
    remove.className = 'badge-remove raised emby-button';
    remove.innerHTML = '<span>Retirer</span>';
    row.append(
        head,
        jellyfinField('Texte', jellyfinInput('badge-text', 'text', badge.Text || '')),
        jellyfinField('Couleur', jellyfinInput('badge-color', 'text', badge.ColorHex || '#C4A35A')),
        jellyfinField('Couleur du texte', jellyfinInput('badge-text-color', 'text', badge.TextColorHex || '#F6F1E7')),
        jellyfinField('Taille', jellyfinInput('badge-size', 'number', badge.SizePercent || 18)),
        jellyfinField('Marqueurs', jellyfinInput('badge-markers', 'text', badge.Markers || ''), 'Vide = badge par défaut'),
        jellyfinCheck('badge-filled', badge.Filled, 'Fond plein'),
        jellyfinCheck('badge-enabled', badge.Enabled !== false, 'Actif'),
        remove
    );
    const paintHead = () => {
        const text = row.querySelector('.badge-text').value.trim() || 'Badge';
        const color = row.querySelector('.badge-color').value.trim() || '#C4A35A';
        const size = row.querySelector('.badge-size').value || '18';
        const markers = row.querySelector('.badge-markers').value.trim();
        row.querySelector('.badge-head').textContent = text + ' · ' + color + ' · taille ' + size + ' · ' + (markers || 'défaut');
    };
    paintHead();
    row.addEventListener('input', paintHead);
    return row;
}

function renderBadges(view, badges) {
    const list = view.querySelector('#BadgeList');
    list.replaceChildren();
    (badges && badges.length ? badges : defaultBadges).forEach((badge) => {
        const row = badgeRow(badge);
        list.appendChild(row);
        upgrade(row);
    });
}

function readBadges(view) {
    return Array.from(view.querySelectorAll('#BadgeList .badge-row')).map((row) => ({
        Text: row.querySelector('.badge-text').value.trim(),
        ColorHex: row.querySelector('.badge-color').value.trim(),
        TextColorHex: row.querySelector('.badge-text-color').value.trim(),
        SizePercent: parseInt(row.querySelector('.badge-size').value, 10) || 18,
        Markers: row.querySelector('.badge-markers').value.trim(),
        Filled: row.querySelector('.badge-filled').checked,
        Enabled: row.querySelector('.badge-enabled').checked
    })).filter((badge) => badge.Text);
}

function loadConfig(view) {
    if (!window.ApiClient) {
        renderBadges(view, defaultBadges);
        return;
    }

    ApiClient.getPluginConfiguration(pluginId).then((config) => {
        let saved = config.Badges || config.badges;
        if ((!saved || !saved.length) && config.BadgesJson) {
            try {
                saved = JSON.parse(config.BadgesJson);
            } catch (ex) {
                saved = null;
            }
        }
        renderBadges(view, saved && saved.length ? saved : defaultBadges);
        view.querySelector('#EnableForMovies').checked = config.EnableForMovies !== false;
        view.querySelector('#EnableForEpisodes').checked = config.EnableForEpisodes !== false;
        view.querySelector('#FrenchLanguageCodes').value = config.FrenchLanguageCodes || 'fre,fra,fr,french,français,francais';
        view.querySelector('#ClearMarkers').value = config.ClearMarkers || '';
    });
}

export default function (view) {
    loadConfig(view);
    view.addEventListener('viewshow', () => {
        loadConfig(view);
    });

    view.querySelector('#AddBadge').addEventListener('click', (event) => {
        event.preventDefault();
        event.stopPropagation();
        const row = badgeRow({
            Text: 'NOUVEAU',
            ColorHex: '#C45A5A',
            TextColorHex: '#F6F1E7',
            SizePercent: 18,
            Markers: '',
            Filled: true,
            Enabled: true
        });
        view.querySelector('#BadgeList').appendChild(row);
        upgrade(row);
    });

    view.addEventListener('click', (event) => {
        const remove = event.target.closest && event.target.closest('.badge-remove');
        if (!remove || !view.contains(remove)) {
            return;
        }
        event.preventDefault();
        remove.closest('.badge-row').remove();
    });

    view.querySelector('#VoBadgeConfigForm').addEventListener('submit', (event) => {
        event.preventDefault();
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(pluginId).then((config) => {
            config.Badges = readBadges(view);
            config.BadgesJson = JSON.stringify(config.Badges);
            config.EnableForMovies = view.querySelector('#EnableForMovies').checked;
            config.EnableForEpisodes = view.querySelector('#EnableForEpisodes').checked;
            config.FrenchLanguageCodes = view.querySelector('#FrenchLanguageCodes').value;
            config.ClearMarkers = view.querySelector('#ClearMarkers').value;
            ApiClient.updatePluginConfiguration(pluginId, config).then((result) => {
                Dashboard.processPluginConfigurationUpdateResult(result);
            });
        });
        return false;
    });
}
