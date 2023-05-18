// see CDN example https://mermaid.js.org/intro/n00b-gettingStarted.html#_3-calling-the-javascript-api
import mermaid from 'https://unpkg.com/mermaid@10.5.0/dist/mermaid.esm.min.mjs';

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
        toggle,

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

            // ...and removes it. Note this timeout has to match the animation duration for '.leaving' in the .less file.
            setTimeout(() => { toast.remove(); }, 1000);
        }, 5000);
    };
})();

const output = (function () {
    const output = getById('output'),
        hasSVG = () => output.childElementCount > 0,
        getSVG = () => hasSVG() ? output.children[0] : null,

        updateSvgViewBox = (svg, viewBox) => {
            if (svg.originalViewBox === undefined) {
                const vb = svg.viewBox.baseVal;
                svg.originalViewBox = { x: vb.x, y: vb.y, width: vb.width, height: vb.height, };
            }

            svg.setAttribute('viewBox', `${viewBox.x} ${viewBox.y} ${viewBox.width} ${viewBox.height}`);
        };

    // enable zooming SVG using Ctrl + mouse wheel
    const zoomFactor = 0.1, panFactor = 2023; // to go with the Zeitgeist

    output.addEventListener('wheel', event => {
        if (!event.ctrlKey || !hasSVG()) return;
        event.preventDefault();

        const svg = getSVG(),
            delta = event.deltaY < 0 ? 1 : -1,
            zoomDelta = 1 + zoomFactor * delta,
            viewBox = svg.viewBox.baseVal;

        viewBox.width *= zoomDelta;
        viewBox.height *= zoomDelta;
        updateSvgViewBox(svg, viewBox);
    });

    // enable panning SVG by grabbing and dragging
    let isPanning = false, panStartX = 0, panStartY = 0;

    output.addEventListener('mousedown', event => {
        isPanning = true;
        panStartX = event.clientX;
        panStartY = event.clientY;
    });

    output.addEventListener('mouseup', () => { isPanning = false; });

    output.addEventListener('mousemove', event => {
        if (!isPanning || !hasSVG()) return;
        event.preventDefault();

        const svg = getSVG(),
            viewBox = svg.viewBox.baseVal,
            dx = event.clientX - panStartX,
            dy = event.clientY - panStartY;

        viewBox.x -= dx * panFactor / viewBox.width;
        viewBox.y -= dy * panFactor / viewBox.height;
        panStartX = event.clientX;
        panStartY = event.clientY;
        updateSvgViewBox(svg, viewBox);
    });

    return {
        getDiagramTitle: () => output.dataset.title,
        setSVG: svg => { output.innerHTML = svg; },
        getSVG,

        resetZoomAndPan: () => {
            const svg = getSVG();
            if (svg !== null) updateSvgViewBox(svg, svg.originalViewBox);
        }
    };
})();

const mermaidExtensions = (() => {

    const logLevel = (() => {
        /* int indexes as well as string values can identify a valid log level;
            see log levels and logger definition at https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/src/logger.ts .
            Note the names correspond to console output methods https://developer.mozilla.org/en-US/docs/Web/API/console .*/
        const names = ['trace', 'debug', 'info', 'warn', 'error', 'fatal'],
            maxIndex = names.length - 1,

            getIndex = level => {
                const index = Number.isInteger(level) ? level : names.indexOf(level);
                return index < 0 ? maxIndex : Math.min(index, maxIndex); // normalize, but return maxIndex (i.e. lowest level) by default
            };

        let requested; // the log level index of the in-coming config or the default

        return {
            /** Sets the desired log level.
             * @param {string|int} level The name or index of the desired log level. */
            setRequested: level => { requested = getIndex(level); },

            /** Returns all names above (not including) the given level.
             * @param {int} level The excluded lower boundary log level index (not name).
             * @returns an array. */
            above: level => names.slice(level + 1),

            /** Indicates whether the log level is configured to be enabled.
             * @param {string|int} level The log level to test.
             * @returns a boolean. */
            isEnabled: level => requested <= getIndex(level)
        };
    })();

    /** Calculates the shortest distance in pixels between a point
     *  represented by 'top' and 'left' and the closest side of an axis-aligned rectangle.
     *  Returns 0 if the point is inside or on the edge of the rectangle.
     *  Inspired by https://gamedev.stackexchange.com/a/50722 .
     *  @param {int} top The distance of the point from the top of the viewport.
     *  @param {int} left The distance of the point from the left of the viewport.
     *  @param {DOMRect} rect The bounding box to get the distance to.
     *  @returns {int} The distance of the outside point or 0. */
    function getDistanceToRect(top, left, rect) {
        const dx = Math.max(rect.left, Math.min(left, rect.right)),
            dy = Math.max(rect.top, Math.min(top, rect.bottom));

        return Math.sqrt((left - dx) * (left - dx) + (top - dy) * (top - dy));
    }

    /** Calculates the distance between two non-overlapping axis-aligned rectangles.
     *  Returns 0 if the rectangles touch or overlap.
     *  @param {DOMRect} a The first bounding box.
     *  @param {DOMRect} b The second bounding box.
     *  @returns {int} The distance between the two bounding boxes or 0 if they touch or overlap. */
    function getDistance(a, b) {
        /** Gets coordinate pairs for the corners of a rectangle r.
         * @param {DOMRect} r the rectangle.
         * @returns {Array}} */
        const getCorners = r => [[r.top, r.left], [r.top, r.right], [r.bottom, r.left], [r.bottom, r.right]],
            /** Gets the distances of the corners of rectA to rectB. */
            getCornerDistances = (rectA, rectB) => getCorners(rectA).map(c => getDistanceToRect(c[0], c[1], rectB)),
            aRect = a.getBoundingClientRect(),
            bRect = b.getBoundingClientRect(),
            cornerDistances = getCornerDistances(aRect, bRect).concat(getCornerDistances(bRect, aRect));

        return Math.min(...cornerDistances);
    }

    function interceptConsole(interceptorsByLevel) {
        const originals = {};

        for (let [level, interceptor] of Object.entries(interceptorsByLevel)) {
            if (typeof console[level] !== 'function') continue;
            originals[level] = console[level];
            console[level] = function () { interceptor.call(this, originals[level], arguments); };
        }

        return () => { // call to detach interceptors
            for (let [level, original] of Object.entries(originals))
                console[level] = original;
        };
    }

    let renderedEdges = []; // contains info about the arrows between types on the diagram once rendered

    function getRelationLabels(svg, type) {
        const edgeLabels = [...svg.querySelectorAll('.edgeLabels span.edgeLabel span')],
            extension = 'extension';

        return renderedEdges.filter(e => e.v === type // type name needs to match
            && e.value.arrowTypeStart !== extension && e.value.arrowTypeEnd !== extension) // exclude inheritance arrows
            .map(edge => {
                const labelHtml = edge.value.label,
                    // filter edge labels with matching HTML
                    labels = edgeLabels.filter(l => l.outerHTML === labelHtml);

                if (labels.length === 1) return labels[0]; // return the only matching label
                else if (labels.length < 1) console.error(
                    "Tried to find a relation label for the following edge (by its value.label) but couldn't.", edge);
                else { // there are multiple edge labels with the same HTML (i.e. matching relation name)
                    // find the path that is rendered for the edge
                    const path = svg.querySelector('.edgePaths>path.relation#' + edge.value.id),
                        labelsByDistance = labels.sort((a, b) => getDistance(path, a) - getDistance(path, b));

                    console.warn('Found multiple relation labels matching the following edge (by its value.label). Returning the closest/first.',
                        edge, labelsByDistance);

                    return labelsByDistance[0]; // and return the matching label closest to it
                }
            });
    }

    return {
        init: config => {

            /* Override console.info to intercept a message posted by mermaid including information about the edges
                (represented by arrows between types in the rendered diagram) to access the relationship info
                parsed from the diagram descriptions of selected types.
                This works around the mermaid API currently not providing access to this information
                and it being hard to reconstruct from the rendered SVG alone.
                Why do we need that info? Knowing about the relationships between types, we can find the label
                corresponding to a relation and attach XML documentation information to it, if available.
                See how getRelationLabels is used. */
            const interceptors = {
                info: function (overridden, args) {
                    // intercept message containing rendered edges
                    if (args[2] === 'Graph in recursive render: XXX') renderedEdges = args[3].edges;

                    // only foward to overridden method if this log level was originally enabled
                    if (logLevel.isEnabled(2)) overridden.call(this, ...args);
                }
            };

            logLevel.setRequested(config.logLevel); // remember original log level
            const requiredLevel = 2; // to enable intercepting info message above

            // lower configured log level if required to guarantee above interceptor gets called
            if (!logLevel.isEnabled(requiredLevel)) config.logLevel = requiredLevel;

            // suppress console output for higher log levels accidentally activated by lowering to required level
            for (let level of logLevel.above(requiredLevel))
                if (!logLevel.isEnabled(level)) interceptors[level] = () => { };

            const detachInterceptors = interceptConsole(interceptors); // attaches console interceptors
            mermaid.initialize(config); // init the mermaid sub-system with interceptors in place
            detachInterceptors(); // to avoid intercepting messages outside of that context we're not interested in
        },

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
                detailedTypes = Object.keys(typeDetails),
                xmlDocs = {}; // to be appended with docs of selected types below

            // init diagram code with header and layout direction to be appended to below
            let diagram = 'classDiagram' + '\n'
                + 'accTitle: ' + output.getDiagramTitle() + '\n'
                + 'direction ' + direction + '\n\n';

            // process selected types
            for (let [type, details] of Object.entries(typeDetails)) {
                diagram += details.DiagramDefinition + '\n\n';

                if (details.InheritedMembersByDeclaringType) {
                    const ancestorTypes = getAncestorTypes(details);

                    // exclude inherited members from sub classes if they are already rendered in a super class
                    for (let [ancestorType, members] of Object.entries(details.InheritedMembersByDeclaringType)) {
                        if (detailedTypes.includes(ancestorType)) continue; // inherited members will be rendered in base type

                        // find inherited props already displayed by detailed base types
                        let renderedInheritedProps = ancestorTypes.filter(t => detailedTypes.includes(t)) // get detailed ancestor types
                            .map(type => getAncestorTypes(typeDetails[type])) // select their ancestor types
                            .reduce((union, ancestors) => union.concat(ancestors), []); // squash them into a one-dimensional array (ignoring duplicates)

                        if (renderedInheritedProps.includes(ancestorType)) continue;
                        diagram += members + '\n';
                    }
                }

                xmlDocs[type] = details.XmlDocs;
            }

            if (filterRegex !== null) diagram = diagram.replace(filterRegex, '');

            return { diagram, detailedTypes, xmlDocs };
        },

        postProcess: (svg, options) => {
            for (let entity of svg.querySelectorAll('g.nodes>g').values()) {
                const title = entity.querySelector('.classTitle'),
                    name = title.textContent,
                    docs = structuredClone((options.xmlDocs || [])[name]); // clone to have a modifyable collection without affecting the original

                // splice in XML documentation as label titles if available
                if (docs) {
                    const typeKey = '', nodeLabel = 'span.nodeLabel',
                        relationLabels = getRelationLabels(svg, name),

                        setDocs = (label, member) => {
                            label.title = docs[member];
                            delete docs[member];
                        },

                        documentOwnLabel = (label, member) => {
                            setDocs(label, member);
                            ownLabels = ownLabels.filter(l => l !== label); // remove label
                        };

                    let ownLabels = [...entity.querySelectorAll('g.label ' + nodeLabel)];

                    // document the type label itself
                    if (hasProperty(docs, typeKey)) documentOwnLabel(title.querySelector(nodeLabel), typeKey);

                    // loop through documented members longest name first
                    for (let member of Object.keys(docs).sort((a, b) => b.length - a.length)) {
                        // matches only whole words in front of method signatures starting with (
                        const memberName = new RegExp(`(?<!.*\\(.*)\\b${member}\\b`),
                            matchingLabels = ownLabels.filter(l => memberName.test(l.textContent)),
                            related = relationLabels.find(l => l.textContent === member);

                        if (related) matchingLabels.push(related);

                        if (matchingLabels.length === 0) console.error(
                            `Expected to find either a member or relation label for ${name}.${member} to attach the XML documentation to but found none.`);
                        else if (matchingLabels.length > 1) console.error(
                            `Expected to find one member or relation label for ${name}.${member} to attach the XML documentation to but found multiple. Applying the first.`, matchingLabels);
                        else documentOwnLabel(matchingLabels[0], member);
                    }
                }

                if (typeof options.onTypeClick === 'function') entity.addEventListener('click',
                    function (event) { options.onTypeClick.call(this, event, name); });
            }
        }
    };
})();

const state = (() => {
    const originalTitle = document.head.getElementsByTagName('title')[0].textContent;

    const restore = async data => {
        if (data.d) layoutDirection.set(data.d);

        if (data.t) {
            typeSelector.setSelected(data.t);
            await render(true);
        }
    };

    function updateQueryString(href, params) {
        // see https://developer.mozilla.org/en-US/docs/Web/API/URL
        const url = new URL(href), search = url.searchParams;

        for (const [name, value] of Object.entries(params)) {
            //see https://developer.mozilla.org/en-US/docs/Web/API/URLSearchParams
            if (value === null || value === undefined || value === '') search.delete(name);
            else if (Array.isArray(value)) {
                search.delete(name);
                for (let item of value) search.append(name, item);
            }
            else search.set(name, value);
        }

        url.search = search.toString();
        return url.href;
    }

    window.onpopstate = async event => { await restore(event.state); };

    return {
        update: () => {
            const types = typeSelector.getSelected(),
                t = Object.keys(types),
                d = layoutDirection.get(),
                data = { t, d },
                typeNames = Object.values(types).map(t => t.Name);

            history.pushState(data, '', updateQueryString(location.href, data));

            // record selected types in title so users see which selection they return to when using a history link
            document.title = (typeNames.length ? typeNames.join(', ') + ' - ' : '') + originalTitle;
        },
        restore: async () => {
            const search = new URLSearchParams(location.search);
            await restore({ d: search.get('d'), t: search.getAll('t') });
        }
    };
})();

const typeSelector = (() => {
    const select = getById('type-select'),
        renderBtn = getById('render'),
        typeDefsByNamespace = JSON.parse(getById('typeDefinitionsByNamespace').innerHTML),
        tags = { optgroup: 'OPTGROUP', option: 'option' },
        getOption = typeId => select.querySelector(tags.option + `[value='${typeId}']`);

    // fill select list
    for (let [namespace, types] of Object.entries(typeDefsByNamespace)) {
        let optionParent;

        if (namespace) {
            const group = document.createElement(tags.optgroup);
            group.label = namespace;
            select.appendChild(group);
            optionParent = group;
        } else optionParent = select;

        for (let type of Object.keys(types)) {
            const option = document.createElement(tags.option);
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
            const option = getOption(name);

            if (option !== null) {
                option.selected = !option.selected;
                triggerChangeOn(select);
            }
        },

        /** Returns the types selected by the user in the form of an object with the type names for keys
         *  and objects with the data structure of MermaidClassDiagrammer.Namespace.Type (excluding the Name) for values. */
        getSelected: () => Object.fromEntries([...select.selectedOptions].map(option => {
            const namespace = option.parentElement.nodeName === tags.optgroup ? option.parentElement.label : '',
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
            if (event !== undefined) event.preventDefault();
        }
    };
})();

const render = async isRestoringState => {
    const { diagram, detailedTypes, xmlDocs } = mermaidExtensions.processTypes(
        typeSelector.getSelected(), layoutDirection.get(), baseTypeInheritanceFilter.getRegex());

    console.info(diagram);

    /* Renders response and deconstructs returned object because we're only interested in the svg.
        Note that the ID supplied as the first argument must not match any existing element ID
        unless you want its contents to be replaced. See https://mermaid.js.org/config/usage.html#api-usage */
    const { svg } = await mermaid.render('foo', diagram);
    output.setSVG(svg);

    mermaidExtensions.postProcess(output.getSVG(), {
        xmlDocs,

        onTypeClick: async (event, name) => {
            // toggle selection and re-render on clicking entity
            typeSelector.toggleOption(name);
            await render();
        }
    });

    exportOptions.enable(detailedTypes.length > 0);
    if (!isRestoringState) state.update();
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

    const toBase64 = utf8String => {
        const bytes = new TextEncoder().encode(utf8String);
        return window.btoa(String.fromCharCode.apply(null, bytes));
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
            // allow the default for copying text if no types are rendered, using toggle visibility as indicator
            if (toggle.hidden) return;

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
            case '0': output.resetZoomAndPan(); return;
        }
    }

    if (event.altKey) {
        // enable moving selected types up and down using arrow keys while holding [Alt]
        const upOrDown = event.key === arrowUp ? true : event.key === arrowDown ? false : null;

        if (upOrDown !== null) {
            typeSelector.focus();
            typeSelector.moveSelection(upOrDown);
            event.preventDefault();
            return;
        }
    }
};

mermaidExtensions.init({ startOnLoad: false }); // initializes mermaid as well
typeSelector.focus(); // focus type filter initially to enable keyboard input
await state.restore();