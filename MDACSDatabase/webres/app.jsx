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

class MDACSDataItem extends React.Component {
    constructor(props) {
        super(props);
    }

    render() {
    }
}

class MDACSDataView extends React.Component {
    constructor(props) {
        super(props)
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

ReactDOM.render(
    <MDACSLoginApp
        authUrl="http://localhost:34002"
        dbUrl="http://localhost:34001"
        jugglerUrl=""
    />,
    document.getElementById('root')
);