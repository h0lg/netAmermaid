// see CDN example https://mermaid.js.org/intro/n00b-gettingStarted.html#_3-calling-the-javascript-api
import mermaid from 'https://unpkg.com/mermaid@10.0.2/dist/mermaid.esm.min.mjs';
import { toBase64 } from 'https://unpkg.com/js-base64@3.7.5/base64.mjs';

const getById = id => document.getElementById(id),
    triggerChangeOn = element => { element.dispatchEvent(new Event('change')); },
    hasProperty = (obj, name) => Object.prototype.hasOwnProperty.call(obj, name);

const radios = (() => {
    const checked = ":checked",
        inputsByName = name => `input[name=${name}]`,
        getInput = (name, filter, context) => (context || document).querySelector(inputsByName(name) + filter),
        getInputs = (name, context) => (context || document).querySelectorAll(inputsByName(name));

    return {
        getValue: (name, context) => getInput(name, checked, context).value,

        onChange: (name, handle, context) => {
            for (let radio of getInputs(name, context)) radio.onchange = handle;
        },

        setChecked: (name, value, context) => {
            const radio = getInput(name, `[value="${value}"]`, context);
            radio.checked = true;
            triggerChangeOn(radio);
        }
    };
})();

const collapse = (() => {
    const open = 'open',
        isOpen = element => element.classList.contains(open),

        /** Toggles the open class on the collapse.
         *  @param {HTMLElement} element The collapse to toggle.
         *  @param {boolean} force The state to force. */
        toggle = (element, force) => element.classList.toggle(open, force);

    return {
        isOpen, toggle,

        open: element => {
            if (isOpen(element)) return false; // return whether collapse was opened by this process
            return toggle(element, true);
        }
    };
})();

const notify = (() => {
    const toaster = getById('toaster');

    return message => {
        const toast = document.createElement('span');
        toast.innerText = message;
        toaster.appendChild(toast); // fades in the message

        setTimeout(() => {
            toast.classList.add('leaving'); // fades out the message

            // ...and removes it. Note this timeout has to match the animation duration for '.leaving' in the css.
            setTimeout(() => { toast.remove(); }, 1000);
        }, 5000);
    };
})();

const mermaidExtensions = (() => {
    return {
        /**
         * 
         * @param {object} typeDetails An object with the names of types to display in detail (i.e. with members) for keys
         * and objects with the data structure of MermaidClassDiagrammer.Namespace.Type (excluding the Name) for values.
         * @param {string} direction The layout direction of the resulting diagram
         * @param {string|RegExp} filterRegex A regular expression matching things to exclude from the diagram definition.
         * @returns
         */
        processTypes: (typeDetails, direction, filterRegex) => {
            const getAncestorTypes = typeDetails => Object.keys(typeDetails.InheritedMembersByDeclaringType),
                detailedTypes = Object.keys(typeDetails);

            // init diagram code with header and layout direction to be appended to below
            let diagram = 'classDiagram' + '\n'
                + 'direction ' + direction + '\n\n';

            // process selected types
            for (let details of Object.entries(typeDetails)) {
                diagram += details.DiagramDefinition + '\n\n';

                if (details.InheritedMembersByDeclaringType) {
                    const ancestorTypes = getAncestorTypes(details);

                    // exclude inherited members from sub classes if they are already rendered in a super class
                    for (let [ancestorType, members] of Object.entries(details.InheritedMembersByDeclaringType)) {
                        if (detailedTypes.includes(ancestorType)) continue; // inherited members will be rendered in base type

                        // find inherited props already displays by detailed base types
                        let renderedInheritedProps = ancestorTypes.filter(t => detailedTypes.includes(t)) // get detailed ancestor types
                            .map(type => getAncestorTypes(typeDetails[type])) // select their ancestor types
                            .reduce((union, ancestors) => union.concat(ancestors), []); // squash them into a one-dimensional array (ignoring duplicates)

                        if (renderedInheritedProps.includes(ancestorType)) continue;
                        diagram += members + '\n';
                    }
                }
            }

            if (filterRegex !== null) diagram = diagram.replace(filterRegex, '');

            return { diagram, detailedTypes };
        },

        postProcess: (svgParent, options) => {
            for (let entity of svgParent.querySelectorAll('g.nodes>g').values()) {
                const title = entity.querySelector('.classTitle'),
                    name = title.textContent;

                if (typeof options.onTypeClick === 'function') entity.addEventListener('click',
                    function (event) { options.onTypeClick.call(this, event, name); });
            }
        }
    };
})();

const typeFilter = (() => {
    const select = getById('typeFilter'),
        renderBtn = getById('render'),
        typeDefsByNamespace = JSON.parse(getById('typeDefinitionsByNamespace').innerHTML);

    // fill select list
    for (let [namespace, types] of Object.entries(typeDefsByNamespace)) {
        let optionParent;

        if (namespace) {
            const group = document.createElement('optgroup');
            group.label = namespace;
            select.appendChild(group);
            optionParent = group;
        } else optionParent = select;

        for (let type of Object.keys(types)) {
            const option = document.createElement('option');
            option.innerText = option.value = type;
            optionParent.appendChild(option);
        }
    }

    // only enable render button if types are selected
    select.onchange = () => { renderBtn.disabled = select.selectedOptions.length < 1; };

    return {
        focus: () => select.focus(),

        setSelected: types => {
            for (let option of select.options)
                option.selected = types.includes(option.value);

            triggerChangeOn(select);
        },

        toggleOption: name => {
            const option = select.querySelector(`option[value='${name}']`);

            if (option !== null) {
                option.selected = !option.selected;
                triggerChangeOn(select);
            }
        },

        /** Returns the types selected by the user in the form of an object with the type names for keys
         *  and objects with the data structure of MermaidClassDiagrammer.Namespace.Type (excluding the Name) for values. */
        getSelected: () => Object.fromEntries([...select.selectedOptions].map(option => {
            const namespace = option.parentElement.nodeName === 'OPTGROUP' ? option.parentElement.label : '',
                type = option.value,
                details = typeDefsByNamespace[namespace][type];

            return [type, details];
        })),

        moveSelection: up => {
            // inspired by https://stackoverflow.com/a/25851154
            for (let option of select.selectedOptions) {
                if (up && option.previousElementSibling) { // move up
                    option.parentElement.insertBefore(option, option.previousElementSibling);
                } else if (!up && option.nextElementSibling) { // move down
                    // see https://developer.mozilla.org/en-US/docs/Web/API/Node/insertBefore
                    option.parentElement.insertBefore(option, option.nextElementSibling.nextElementSibling);
                }
            }
        }
    };
})();

const lastRenderedTypes = (() => {
    let types;

    const hasAny = () => types.length > 0,
        restore = getById('restore-last-rendered'),

        set = values => {
            types = values;
            restore.hidden = !hasAny();
        };

    set([]); // to initialize

    // enable restoring last rendered type selection
    restore.onclick = () => {
        typeFilter.setSelected(types);
        typeFilter.focus(); // re-focus to make continuing work easier
    };

    return { set, hasAny };
})();

const baseTypeInheritanceFilter = (() => {
    const checkbox = getById('show-base-types'),
        baseTypeRegex = checkbox.dataset.baseTypeRegex,
        hasRegex = baseTypeRegex.length > 0,

        /* matches expressions for inheritance from common base types
            (the entire line including the ending line break for clean replacement)
            see https://stackoverflow.com/a/4029123 on how to insert variables into regex */
        inheritanceRegex = hasRegex ? new RegExp(`^${baseTypeRegex}<\\|--\\w+[\\r]?\\n`, 'gm') : null;

    // hide show base type filter and label if no base type regex is supplied
    checkbox.hidden = !hasRegex;
    for (let label of checkbox.labels) label.hidden = !hasRegex;

    return { getRegex: () => inheritanceRegex !== null && !checkbox.checked ? inheritanceRegex : null };
})();

const layoutDirection = (() => {
    const inputName = 'direction';

    radios.onChange(inputName, async () => { await render(); });

    return {
        get: () => radios.getValue(inputName),
        set: (value, event) => {
            radios.setChecked(inputName, value);
            event.preventDefault();
        }
    };
})();

const render = async () => {
    const { diagram, detailedTypes } = mermaidExtensions.processTypes(
        typeFilter.getSelected(), layoutDirection.get(), baseTypeInheritanceFilter.getRegex());

    console.info(diagram);

    /* Renders response and deconstructs returned object because we're only interested in the svg.
        Note that the ID supplied as the first argument must not match any existing element ID
        unless you want its contents to be replaced. See https://mermaid.js.org/config/usage.html#api-usage */
    const { svg } = await mermaid.render('foo', diagram),
        output = getById('output');

    output.innerHTML = svg;

    mermaidExtensions.postProcess(output, {
        onTypeClick: async (event, name) => {
            // toggle selection and re-render on clicking entity
            typeFilter.toggleOption(name);
            await render();
        }
    });

    lastRenderedTypes.set(detailedTypes);
    exportOptions.enable(detailedTypes.length > 0);
};

const filterSidebar = (() => {
    const filterForm = getById('filter'),
        toggle = () => collapse.toggle(filterForm);

    // enable rendering by hitting Enter on filter form
    filterForm.onsubmit = async (event) => {
        event.preventDefault();
        await render();
    };

    // enable toggling filter info
    getById('info-toggle').onclick = () => { collapse.toggle(getById('info')); };
    getById('filter-toggle').onclick = toggle; // toggle sidebar on click

    return {
        toggle,
        open: () => collapse.open(filterForm)
    };
})();

/* Shamelessly copied from https://github.com/mermaid-js/mermaid-live-editor/blob/develop/src/lib/components/Actions.svelte
    with only a few modifications after I failed to get the solutions described here working:
    https://stackoverflow.com/questions/28226677/save-inline-svg-as-jpeg-png-svg/28226736#28226736
    The closest I got was with this example https://canvg.js.org/examples/offscreen , but the shapes would remain empty. */
const exporter = (() => {
    const getBase64SVG = (svg, width, height) => {
        height && svg?.setAttribute('height', `${height}px`);
        width && svg?.setAttribute('width', `${width}px`); // Workaround https://stackoverflow.com/questions/28690643/firefox-error-rendering-an-svg-image-to-html5-canvas-with-drawimage
        if (!svg) {
            svg = getSvgEl();
        }
        const svgString = svg.outerHTML
            .replaceAll('<br>', '<br/>')
            .replaceAll(/<img([^>]*)>/g, (m, g) => `<img ${g} />`);
        return toBase64(svgString);
    };

    const exportImage = (event, exporter, imagemodeselected, userimagesize) => {
        const canvas = document.createElement('canvas');
        const svg = document.querySelector('#output svg');
        if (!svg) {
            throw new Error('svg not found');
        }
        const box = svg.getBoundingClientRect();
        canvas.width = box.width;
        canvas.height = box.height;
        if (imagemodeselected === 'width') {
            const ratio = box.height / box.width;
            canvas.width = userimagesize;
            canvas.height = userimagesize * ratio;
        } else if (imagemodeselected === 'height') {
            const ratio = box.width / box.height;
            canvas.width = userimagesize * ratio;
            canvas.height = userimagesize;
        }
        const context = canvas.getContext('2d');
        if (!context) {
            throw new Error('context not found');
        }
        context.fillStyle = 'white';
        context.fillRect(0, 0, canvas.width, canvas.height);
        const image = new Image();
        image.onload = exporter(context, image);
        image.src = `data:image/svg+xml;base64,${getBase64SVG(svg, canvas.width, canvas.height)}`;
        event.stopPropagation();
        event.preventDefault();
    };

    const getSvgEl = () => {
        const svgEl = document.querySelector('#output svg').cloneNode(true);
        svgEl.setAttribute('xmlns:xlink', 'http://www.w3.org/1999/xlink');
        const fontAwesomeCdnUrl = Array.from(document.head.getElementsByTagName('link'))
            .map((l) => l.href)
            .find((h) => h.includes('font-awesome'));
        if (fontAwesomeCdnUrl == null) {
            return svgEl;
        }
        const styleEl = document.createElement('style');
        styleEl.innerText = `@import url("${fontAwesomeCdnUrl}");'`;
        svgEl.prepend(styleEl);
        return svgEl;
    };

    const simulateDownload = (download, href) => {
        const a = document.createElement('a');
        a.download = download;
        a.href = href;
        a.click();
        a.remove();
    };

    const downloadImage = (context, image) => {
        return () => {
            const { canvas } = context;
            context.drawImage(image, 0, 0, canvas.width, canvas.height);
            simulateDownload(
                exportOptions.getFileName('png'),
                canvas.toDataURL('image/png').replace('image/png', 'image/octet-stream')
            );
        };
    };

    const clipboardCopy = (context, image) => {
        return () => {
            const { canvas } = context;
            context.drawImage(image, 0, 0, canvas.width, canvas.height);
            canvas.toBlob((blob) => {
                try {
                    if (!blob) {
                        throw new Error('blob is empty');
                    }
                    void navigator.clipboard.write([
                        new ClipboardItem({
                            [blob.type]: blob
                        })
                    ]);
                } catch (error) {
                    console.error(error);
                }
            });
        };
    };

    return {
        isClipboardAvailable: () => hasProperty(window, 'ClipboardItem'),
        onCopyClipboard: (event, imagemodeselected, userimagesize) => {
            exportImage(event, clipboardCopy, imagemodeselected, userimagesize);
        },
        onDownloadPNG: (event, imagemodeselected, userimagesize) => {
            exportImage(event, downloadImage, imagemodeselected, userimagesize);
        },
        onDownloadSVG: () => {
            simulateDownload(exportOptions.getFileName('svg'), `data:image/svg+xml;base64,${getBase64SVG()}`);
        }
    };
})();

const exportOptions = (() => {
    let wereOpened = false; // used to track whether user was able to see save options and may quick-save

    const container = getById('exportOptions'),
        toggle = getById('exportOptions-toggle'),
        saveBtn = getById('save'),
        copyBtn = getById('copy'),
        saveAs = 'saveAs',
        png = 'png',

        open = () => {
            wereOpened = true;
            return collapse.open(container);
        },

        copy = event => {
            // allow the default for copying text if no types are rendered
            if (!lastRenderedTypes.hasAny()) return;

            if (!exporter.isClipboardAvailable()) notify('The clipboard seems unavailable in this browser :(');
            else {
                try {
                    exporter.onCopyClipboard(event);
                    notify('An image of the diagram is in your clipboard.');
                } catch (e) {
                    notify(e.toString());
                }
            }
        },

        save = event => {
            if (radios.getValue(saveAs) === png) {
                const [dimension, size] = getDimensions();
                exporter.onDownloadPNG(event, dimension, size);
            }
            else exporter.onDownloadSVG();
        };

    const getDimensions = (() => {
        const inputName = 'dimension',
            dimensions = getById('dimensions'),
            scaleInputs = container.querySelectorAll('#scale-controls input');

        // enable toggling dimension controls
        radios.onChange(saveAs, event => {
            collapse.toggle(dimensions, event.target.value === png);
        }, container);

        // enable toggling scale controls
        radios.onChange(inputName, event => {
            const disabled = event.target.value !== 'scale';
            for (let input of scaleInputs) input.disabled = disabled;
        }, container);

        return () => {
            let dimension = radios.getValue(inputName);

            // return dimension to scale to desired size if not exporting in current size
            if (dimension !== 'auto') dimension = radios.getValue('scale');

            return [dimension, getById('scale-size').value];
        };
    })();

    toggle.onclick = () => collapse.toggle(container);

    if (exporter.isClipboardAvailable()) copyBtn.onclick = copy;
    else copyBtn.hidden = true;

    saveBtn.onclick = save;

    return {
        copy,
        getFileName: ext => `${saveBtn.dataset.assembly}-diagram-${new Date().toISOString().replace(/[Z:.]/g, '')}.${ext}`,

        enable: enable => {
            if (!enable) collapse.toggle(container, false); // make sure the container is closed when disabling
            toggle.hidden = !enable;
        },

        quickSave: (event) => {
            if (toggle.hidden) return; // saving is not enabled

            if (wereOpened) {
                save(event); // allow quick save
                return;
            }

            const filterOpened = filterSidebar.open(),
                optionsOpenend = open();

            /* Make sure the collpases containing the save options are open and visible when user hits Ctrl + S.
                If neither needed opening, trigger saving. I.e. hitting Ctrl + S again should do it. */
            if (!filterOpened && !optionsOpenend) save(event);
            else event.preventDefault(); // prevent saving HTML page
        }
    };
})();

// key bindings
document.onkeydown = async (event) => {
    const arrowUp = 'ArrowUp', arrowDown = 'ArrowDown';

    if (event.ctrlKey) {
        switch (event.key) {
            case 'b': filterSidebar.toggle(); return;
            case 's': exportOptions.quickSave(event); return;
            case 'c': exportOptions.copy(event); return;
            case 'ArrowLeft': layoutDirection.set('RL', event); return;
            case 'ArrowRight': layoutDirection.set('LR', event); return;
            case arrowUp: layoutDirection.set('BT', event); return;
            case arrowDown: layoutDirection.set('TB', event); return;
        }
    }

    if (event.altKey) {
        // enable moving selected types up and down using arrow keys while holding [Alt]
        const moveUp = event.key === arrowUp ? true : event.key === arrowDown ? false : null;

        if (moveUp !== null) {
            typeFilter.focus();
            typeFilter.moveSelection(moveUp);
            event.preventDefault();
            return;
        }
    }
};

mermaid.initialize({ startOnLoad: false });
typeFilter.focus(); // focus type filter initially to enable keyboard input
