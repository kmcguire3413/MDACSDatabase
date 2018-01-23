/*
      MDACSLoginApp
        Login
        LoggedIn
          // Controls what major panel is displayed.
          MDACSServiceDirectory
            AuthServicePanel
              *
            StorageJugglerPanel
              *
            DatabaseServicePanel
              DeviceConfigurationList
                DeviceConfiguration
              DataItemSearch
              DataItemList
                DataItem
                  DataItemDateTime
                  DataItemUser
                  DataItemDevice
                  DataItemNote
                  DataItemControls
*/

class MDACSDataViewSummary extends React.Component {
    constructor(props) {
        super(props);
    }

    render() {
    }
}

class MDACSDeviceConfiguration extends React.Component {
    constructor(props) {
        super(props);
    }

    render() {
    }
}

class MDACSConfigurationList extends React.Component {
    constructor(props) {
        super(props) 
    }

    render() {
    }
}

/// <prop name="beginIndex">Only for visual purposes, if needed.</prop>
/// <prop name="endIndex">Only for visual purposes, if needed.</prop>
/// <prop name="data">Used to render the items. The entire data array is rendered.</prop>
class MDACSDataView extends React.Component {
    constructor(props) {
        super(props)
    }

    render() {
        let props = this.props;
        let data = props.data;
        let beginIndex = props.beginIndex;
        let endIndex = props.endIndex;

        if (data === null) {
            return <div>No data.</div>;
        }

        let tmp = [];

        for (let x = 0; x < data.length; ++x) {
            let item = data[x];

            if (item !== undefined && item !== null) {
                tmp.push(<MDACSDataItem index={x + beginIndex} key={item.security_id} item={item}/>);
            }
        }

        let footer = null;

        if (tmp.length === 0) {
            footer = <div>No items from {beginIndex} to {endIndex - 1}.</div>;
        }

        return  <div style={{backgroundColor: 'white'}}>
                <Table striped bordered>
                    <thead>
                        <tr>
                            <td>Index</td>
                            <td>State</td>
                            <td>Date</td>
                            <td>Time</td>
                            <td>User</td>
                            <td>Device</td>
                            <td style={{width: '100%'}}>Note</td>
                        </tr>
                    </thead>
                    <tbody>
                        {tmp}
                    </tbody>
                </Table>
                {footer}
                </div>;
    }
}

const MDACSDatabaseModuleStateGenerator = () => {
    let state = {
        data: null,
        error: null,
        beginIndex: 0,
        endIndex: 50,
        visibleCount: 50,
        dataCount: 0,
        builtInitialSubset: false,
        workerStatus: 'Loading...',
        worker: new Worker('utility?MDACSDataWorker.js'),
        searchValue: '',
    };

    return state;
};

const MDACSDatabaseModuleMutators = {
    onWorkerMessage: (props, state, setState, e) => {
        let msg = e.data;

        switch (msg.topic) {
            case 'LoadDataStringDone':
            {
                // If the initial subset has not been produced
                // then issue the command to build it.
                if (state.builtInitialSubset === false) {
                    setState({
                        builtInitialSubset: true,
                    });

                    state.worker.postMessage({
                        topic: 'ProduceSubSet',
                        criteria: [],
                    });
                }
                break;
            }
            case 'ProduceSubSetDone':
            {
                setState({
                    beginIndex: 0,
                    endIndex: state.visibleCount,
                    dataCount: msg.count,
                });

                state.worker.postMessage({
                    topic: 'GetSubSetOfSubSet',
                    beginIndex: 0,
                    endIndex: state.visibleCount,
                });
                break;
            }
            case 'GetSubSetOfSubSetDone':
            {
                setState({
                    data: msg.subset,
                });
                break;
            }
            case 'Status':
            {
                setState({
                    workerStatus: msg.status,
                });

                console.log('status', msg.status);
                break;
            }
        }
    },
    prevPage: (props, state, setState) => {
        setState((prevState, props) => {
            state.worker.postMessage({
                topic: 'GetSubSetOfSubSet',
                beginIndex: prevState.beginIndex - prevState.visibleCount,
                endIndex: prevState.endIndex - prevState.visibleCount,
            });

            return {
                beginIndex: prevState.beginIndex - prevState.visibleCount,
                endIndex: prevState.endIndex - prevState.visibleCount,
                data: null,
            }
        });
    },
    nextPage: (props, state, setState) => {
        setState((prevState, props) => {
            state.worker.postMessage({
                topic: 'GetSubSetOfSubSet',
                beginIndex: prevState.beginIndex + prevState.visibleCount,
                endIndex: prevState.endIndex + prevState.visibleCount,
            });

            return {
                beginIndex: prevState.beginIndex + prevState.visibleCount,
                endIndex: prevState.endIndex + prevState.visibleCount,
                data: null,
            }
        });
    },
    onSearchChange: (props, state, setState, e) => {
        e.preventDefault();

        setState({
            searchValue: e.target.value,
        });
    },
    onSearchClick: (props, state, setState, e) => {
        e.preventDefault();

        state.worker.postMessage({
            topic: 'ProduceSubSet',
            criteria: state.searchValue.split(' '),
        });
    },
};

const MDACSDatabaseModuleViews = {
    Main: (props, state, setState, mutators) => {
        let errorView;

        const prevPage = () => mutators.prevPage(props, state, setState);
        const nextPage = () => mutators.nextPage(props, state, setState);
        const onSearchChange = (e) => mutators.onSearchChange(props, state, setState, e);
        const onSearchClick = (e) => mutators.onSearchClick(props, state, setState, e);

        if (state.error !== null) {
            errorView = <Alert>{state.error}</Alert>;
        } else {
            errorView = null;
        }

        let bar = <Table>
                    <thead>
                        <tr>
                            <td></td>
                            <td style={{width: '100%'}}></td>
                            <td></td>
                        </tr>
                    </thead>
                    <tbody>
                        <tr>
                            <td>
                                <div style={{ margin: '0.2in' }}>
                                    <input type="submit" onClick={prevPage} value="Prev Page"/>
                                </div>
                                <div style={{ margin: '0.2in' }}>
                                    <input type="submit" onClick={nextPage} value="Next Page"/>
                                </div>
                            </td>
                            <td>
                                <div>
                                    <input
                                        style={{ margin: '0.2in' }}
                                        type="submit"
                                        value="Search"
                                        onClick={onSearchClick}/>
                                    <input 
                                        onChange={onSearchChange}
                                        type="text"
                                        placeholder="Type search words here."
                                        value={state.searchValue}/>
                                </div>
                                <hr/>
                                <div>
                                    <strong>
                                        Showing {state.beginIndex + 1} to {state.endIndex} items out of {state.dataCount} items using the search string "{state.searchValue}".
                                    </strong>
                                </div>
                            </td>
                        </tr>
                    </tbody>
                </Table>;

        return <Panel bsStyle="primary">
                <Panel.Heading>
                    <Panel.Title componentClass="h3">Data View and Edit</Panel.Title>
                </Panel.Heading>
                <Panel.Body>
            <div>
            {bar}
            </div>
            <h3>{state.workerStatus}</h3>
            <h3>{errorView}</h3>
            <MDACSDataView data={state.data} beginIndex={state.beginIndex} endIndex={state.endIndex} />
            <div>
            {bar}
            </div>
        </Panel.Body>
        </Panel>;
    },
};

function generate_sample_data(count) {
    function padleftzero(v) {
        if (v.length == 1) {
            return '0' + v;
        }

        return v;
    }

    function cf(l) {
        let m = Math.floor(Math.random() * l.length);

        return l[m];
    }

    let users = [
        'bob',
        'mark',
        'jill',
        'bill',
        'bryan',
        'josh',
        'kevin',
        'sue',
        'beth',
        'alicia',
        'jasmine',
    ];

    let devices = [
        'gopro3029',
        'bodycam45',
        'laptop293',
        'upload',
        'gopro3832',
        'bodycam86',
    ];

    let tmp = [];

    for (let x = 0; x < count; ++x) {
        let year = padleftzero(Math.floor(Math.random() * 2000 + 1900).toString());
        let month = padleftzero(Math.floor(Math.random() * 12 + 1).toString());
        let day = padleftzero(Math.floor(Math.random() * 25 + 1).toString());
        let hour = padleftzero(Math.floor(Math.random() * 24).toString());
        let minute = padleftzero(Math.floor(Math.random() * 60).toString());
        let second = padleftzero(Math.floor(Math.random() * 60).toString());

        let item = {
            datestr: year + month + day,
            timestr: hour + minute + second,
            userstr: cf(users),
            devicestr: cf(devices),
            note: '',
            security_id: x,
            state: 'Auto-Purge 90-Days Then Cloud 180-Days',
        };

        tmp.push(item);
    }

    tmp = {
        data: tmp,
    };

    return JSON.stringify(tmp);
}

/// <prop name="dao">DAO for the database service.</prop>
class MDACSDatabaseModule extends React.Component {
    constructor(props) {
        super(props);

        this.state = MDACSDatabaseModuleStateGenerator(props);

        this.state.worker.onmessage = 
            (e) => {
                MDACSDatabaseModuleMutators.onWorkerMessage(
                    props,
                    this.state,
                    this.setState.bind(this),
                    e
                );

                //this.forceUpdate();
            };
    }

    componentDidMount() {
        console.log('fetching data');
        this.setState({
            error: null,
            data: null,
        });

        this.props.dao.data_noparse(
            (dataString) => {
                //dataString = generate_sample_data(10000);

                console.log('data fetched', dataString);
                this.setState({
                    error: null,
                    data: null,
                });
                this.state.worker.postMessage({
                    topic: 'LoadDataString',
                    dataString: dataString,
                });
            },
            (res) => {
                console.log('data fetch error', res);
                this.setState({
                    error: 'A problem happened when retrieving data from the server.',
                });
            }
        );        
    }

    componentWillUnmount() {
    }

    render() {
        return MDACSDatabaseModuleViews.Main(
            this.props,
            this.state,
            this.setState.bind(this),
            MDACSDatabaseModuleMutators
        );
    }
}