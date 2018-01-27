/// <prop name="goBack">Called when user has requested to leave this component.</prop>
/// <prop name="dao">The database access object.</prop>
/// <prop name=""
class MDACSItemsView extends React.Component {
    constructor(props) {
        super(props);

        this.state = {
            itemViewStuff: null,
        };
    }

    buildHandler(i, ndx) {
        const props = this.props;
        const state = this.state;
        const setState = this.setState.bind(this);

        return (e, i) => {
            e.preventDefault();

            const src = props.dao.getDownloadUrl(i.security_id);

            const timeDateString = i.jsDate.toLocaleDateString() + ' ' + i.jsDate.toLocaleTimeString();

            switch (i.datatype) {
                case 'mp4':
                {
                    setState({
                        itemViewStuff: <Panel>
                                <h3>
                                    <strong>#{ndx}</strong> {timeDateString} {i.userstr} {i.devicestr}
                                </h3>
                                <div>
                                    The data type is MP4. The video should load below using the HTML5 video player. Here is a
                                    link to download or view the video externally to this page. You may need to right-click the
                                    the link and select save-as to override the default behavior.
                                </div>
                                <div>
                                    <a href={src} target="_blank">{src}</a>
                                </div>
                                <video style={{ width: '100%', height: '100%' }} src={src} controls="true"/>
                            </Panel>
                    });
                    break;
                }
                case 'jpg':
                {
                    setState({
                        itemViewStuff: <Panel>
                            <h3>
                                <strong>#{ndx}</strong> {timeDateString} {i.userstr} {i.devicestr}
                            </h3>
                            <div>
                                The data type is a JPEG image. The image should load below.
                            </div>
                            <img style={{ width: '100%', height: '100%' }} src={src}/>
                            <div>
                                <a href={src} target="_blank">{src}</a>
                            </div>
                        </Panel>
                    });
                    break;
                }
                default: 
                {
                    setState({
                        itemViewStuff: <Panel>
                                <h3>
                                    <strong>#{ndx}</strong> {timeDateString} {i.userstr} {i.devicestr}
                                </h3>
                                <div>
                                    Not sure how to display the {i.datatype} type of data. Here is a link to click
                                    to download it or allow the browser to decide how to display it.
                                </div>         
                                <div>
                                    <a href={src} target="_blank">{src}</a>
                                </div>
                            </Panel>
                    });
                    break;
                }
            }
        };
    }

    render() {
        const props = this.props;
        const state = this.state;
        const items = props.items;
        const goBack = props.goBack;

        let buttonsForItems = [];

        for (let x = 0; x < items.length; ++x) {
            const i = state.currentlyViewingItems[x];
            const handler = buildHandler(i, x);
            const timeDateString = i.jsDate.toLocaleDateString() + ' ' + i.jsDate.toLocaleTimeString();

            buttonsForItems.push(
                    <div key={i.security_id}>
                        <Button onClick={(e) => handler(e, i)}>
                            <strong>#{x}</strong> {timeDateString}
                        </Button>
                    </div>
                );
        }

        const code = <Panel bsStyle="primary">
            <Panel.Heading>
                <Panel.Title componentClass="h3">Item and Item Stack Viewer</Panel.Title>
            </Panel.Heading>
            <Panel.Body>
                <div>
                    <Button onClick={goBack}>Exit</Button>
                </div>
                <Panel>
                    {buttonsForItems}
                </Panel>
                {state.itemViewStuff}
            </Panel.Body>
        </Panel>;
        
        return code;
    }
}